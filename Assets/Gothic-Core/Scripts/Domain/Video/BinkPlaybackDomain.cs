using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Gothic.Core.Bink;

namespace Gothic.Core.Domain.Video
{
    /// <summary>
    /// Playback of one original Gothic Bink video (.bik), without Unity dependencies: BinkVideo decodes raw YUV
    /// frames + PCM audio on its own thread - in the editor (Mono, debug) a frame took longer than 40 ms on the main
    /// thread. The audio goes into a ring buffer, the picture follows the audio clock (or real time if silent).
    /// BinkPlayer shows the frames and streams the audio. One instance per video.
    /// </summary>
    public class BinkPlaybackDomain : IDisposable
    {
        private const int _framesAhead = 12;
        private const float _ringSeconds = 4f;
        private const int _decoderIdleMs = 2;
        private const int _decoderJoinTimeoutMs = 500;

        // The video clock runs on real time and is pulled gently towards the audio play head (AudioSource.timeSamples).
        // Counting the samples Unity pulled from OnAudioRead was ~280 ms ahead and jumpy (it reads big chunks ahead):
        // the clock re-synced every second and skipped 12-18 frames per second. Hard re-sync only after a real hiccup.
        private const double _clockResyncSeconds = 1.0;
        private const double _clockCorrectionPerSecond = 0.5;

        public class DecodedFrame
        {
            public int Index;
            // Position (sample frames) in the audio stream where this frame's audio starts - the frame is shown when
            // that sample is heard. Exact even if a frame's audio packet isn't 1/fps long (e.g. pre-roll).
            public long AudioStart;
            public readonly byte[][] Planes = new byte[3][];
        }

        public readonly int Width;
        public readonly int Height;
        public readonly double Fps;
        public readonly int FrameCount;
        public readonly bool HasAudio;
        public readonly int SampleRate;
        public readonly int Channels;

        public bool IsAudioStarted { get; private set; }
        public bool IsFinished { get; private set; }
        public string DecodeError => _decodeError;

        private Stream _stream;
        private BinkVideo _video;

        // Shared with the decoder thread - always under _frameLock.
        private readonly object _frameLock = new();
        private readonly Queue<DecodedFrame> _decoded = new();
        private readonly Stack<DecodedFrame> _pool = new();
        private double _startTime;

        private double _videoClock;
        // timeSamples is the position handed to the mixer - it's heard after the DSP buffers. The picture waits for it
        // (video was slightly ahead of the sound).
        private double _audioOutputLatency;

        private Thread _decoderThread;
        private volatile bool _isDecoderRunning;
        private volatile bool _isDecodingDone;
        private volatile string _decodeError;

        // Audio ring buffer, read on the audio thread (ReadAudio).
        private readonly object _audioLock = new();
        private readonly float[] _ring;
        private int _ringRead;
        private int _ringCount;
        private long _samplesPlayed;
        private long _audioPushedFrames; // decoder thread

        /// <summary>
        /// Opens the file. Throws if it can't be read as Bink. Without isAudioEnabled the audio tracks are ignored.
        /// </summary>
        public BinkPlaybackDomain(string path, bool isAudioEnabled)
        {
            try
            {
                _stream = new BufferedStream(File.OpenRead(path), 1 << 16);
                _video = new BinkVideo(_stream);
            }
            catch
            {
                DisposeStream();
                throw;
            }

            Width = _video.Width;
            Height = _video.Height;
            Fps = _video.Fps;
            FrameCount = _video.FrameCount;

            if (_video.AudioTracks.Count == 0 || !isAudioEnabled)
                return;

            var track = _video.AudioTracks[0];
            SampleRate = track.SampleRate;
            Channels = track.IsStereo ? 2 : 1;
            _ring = new float[(int)(SampleRate * Channels * _ringSeconds)];
            HasAudio = true;
        }

        /// <summary>
        /// Length (sample frames) for the streaming audio clip: the whole video plus one second.
        /// </summary>
        public int AudioLengthFrames => (int)Math.Ceiling(FrameCount / Fps * SampleRate) + SampleRate;

        /// <summary>
        /// Starts the decoder thread. audioOutputLatency: seconds from the play head until the sample is heard.
        /// </summary>
        public void Start(double time, double audioOutputLatency)
        {
            _startTime = time;
            _audioOutputLatency = audioOutputLatency;
            _isDecoderRunning = true;
            _decoderThread = new Thread(DecodeLoop) { IsBackground = true, Name = "BinkDecoder" };
            _decoderThread.Start();
        }

        /// <summary>
        /// Called once per frame. Returns true when the sound has to start now (a few frames are buffered) - the
        /// picture follows the sound from then on.
        /// </summary>
        public bool TryStartAudio(double time)
        {
            if (!HasAudio || IsAudioStarted)
                return false;

            int decodedCount;
            lock (_frameLock)
                decodedCount = _decoded.Count;

            if (decodedCount < _framesAhead && !_isDecodingDone)
                return false;

            IsAudioStarted = true;
            _startTime = time;
            return true;
        }

        /// <summary>
        /// Called once per frame after TryStartAudio. audioPlayHeadSamples: the audio source's play position, null if
        /// it isn't playing. Returns the frame to show now (or null) - hand it back with ReleaseFrame after uploading.
        /// Sets IsFinished once the last frame's time is over.
        /// </summary>
        public DecodedFrame Update(double time, double deltaTime, long? audioPlayHeadSamples)
        {
            int decodedCount;
            lock (_frameLock)
                decodedCount = _decoded.Count;

            UpdateVideoClock(deltaTime, audioPlayHeadSamples);
            var frame = TakeCurrentFrame(time);

            if (_isDecodingDone && decodedCount == 0 && GetPlaybackTime(time) * Fps >= FrameCount)
                IsFinished = true;

            return frame;
        }

        public void ReleaseFrame(DecodedFrame frame)
        {
            lock (_frameLock)
                _pool.Push(frame);
        }

        /// <summary>
        /// Decoder thread: keeps up to _framesAhead frames (and their audio) ready.
        /// Works on its own reference of the video: Dispose() may clear _video while a frame is still decoded.
        /// </summary>
        private void DecodeLoop()
        {
            var video = _video;
            try
            {
                while (_isDecoderRunning && video.CurrentFrame < video.FrameCount)
                {
                    DecodedFrame decoded = null;
                    lock (_frameLock)
                    {
                        if (_decoded.Count < _framesAhead)
                            decoded = _pool.Count > 0 ? _pool.Pop() : new DecodedFrame();
                    }

                    if (decoded == null || !HasAudioRoom())
                    {
                        if (decoded != null)
                            ReleaseFrame(decoded);
                        Thread.Sleep(_decoderIdleMs);
                        continue;
                    }

                    decoded.Index = video.CurrentFrame;
                    var frame = video.NextFrame();
                    for (var plane = 0; plane < 3; plane++)
                    {
                        var source = frame.Planes[plane].Data;
                        if (decoded.Planes[plane] == null || decoded.Planes[plane].Length != source.Length)
                            decoded.Planes[plane] = new byte[source.Length];
                        Buffer.BlockCopy(source, 0, decoded.Planes[plane], 0, source.Length);
                    }

                    decoded.AudioStart = _audioPushedFrames;
                    if (HasAudio)
                    {
                        var samples = frame.Audio[0];
                        PushAudio(samples);
                        _audioPushedFrames += samples.Count / Channels;
                    }

                    lock (_frameLock)
                        _decoded.Enqueue(decoded);
                }
            }
            catch (Exception e)
            {
                if (_isDecoderRunning)
                    _decodeError = $"frame {video.CurrentFrame}: {e.Message}";
            }
            finally
            {
                _isDecodingDone = true;
                // Dispose() couldn't wait for this thread (join timed out) and left the stream to it.
                if (!_isDecoderRunning)
                    DisposeStream();
            }
        }

        private bool HasAudioRoom()
        {
            if (!HasAudio)
                return true;
            // One video frame of samples (with margin) has to fit into the ring.
            var frameSamples = (int)(SampleRate * Channels / Fps) * 2;
            lock (_audioLock)
                return _ring.Length - _ringCount >= frameSamples;
        }

        private DecodedFrame TakeCurrentFrame(double time)
        {
            if (HasAudio && !IsAudioStarted)
                return null;

            var playbackTime = GetPlaybackTime(time);
            var targetIndex = (int)(playbackTime * Fps);
            // With sound: the frame whose audio is being heard. Behind -> older frames are skipped, ahead -> it waits.
            var heardSample = (long)(playbackTime * SampleRate);
            DecodedFrame toShow = null;
            lock (_frameLock)
            {
                while (_decoded.Count > 0 && (HasAudio
                           ? _decoded.Peek().AudioStart <= heardSample
                           : _decoded.Peek().Index <= targetIndex))
                {
                    if (toShow != null)
                        _pool.Push(toShow);
                    toShow = _decoded.Dequeue();
                }
            }

            return toShow;
        }

        private double GetPlaybackTime(double time)
        {
            return HasAudio
                ? Math.Max(0.0, _videoClock - _audioOutputLatency)
                : time - _startTime;
        }

        private void UpdateVideoClock(double deltaTime, long? audioPlayHeadSamples)
        {
            if (!HasAudio || !IsAudioStarted)
                return;

            double audioTime;
            if (audioPlayHeadSamples.HasValue)
            {
                audioTime = (double)audioPlayHeadSamples.Value / SampleRate;
            }
            else
            {
                lock (_audioLock)
                    audioTime = (double)_samplesPlayed / (SampleRate * Channels);
            }

            _videoClock += deltaTime;
            var drift = audioTime - _videoClock;
            if (Math.Abs(drift) > _clockResyncSeconds)
                _videoClock = audioTime;
            else
                _videoClock += drift * Math.Min(1.0, _clockCorrectionPerSecond * deltaTime);
        }

        private void PushAudio(List<float> samples)
        {
            lock (_audioLock)
            {
                foreach (var sample in samples)
                {
                    if (_ringCount == _ring.Length)
                        break; // buffer full - should not happen with a few frames ahead
                    _ring[(_ringRead + _ringCount) % _ring.Length] = sample;
                    _ringCount++;
                }
            }
        }

        /// <summary>
        /// Audio thread. Missing samples (decoder behind) are silence.
        /// </summary>
        public void ReadAudio(float[] data)
        {
            lock (_audioLock)
            {
                for (var i = 0; i < data.Length; i++)
                {
                    if (_ringCount > 0)
                    {
                        data[i] = _ring[_ringRead];
                        _ringRead = (_ringRead + 1) % _ring.Length;
                        _ringCount--;
                        _samplesPlayed++;
                    }
                    else
                    {
                        data[i] = 0f;
                        // Keep the clock running at the end so the last frames are shown.
                        if (_isDecodingDone)
                            _samplesPlayed++;
                    }
                }
            }
        }

        /// <summary>
        /// Stops the decoder thread. Safe to call twice.
        /// </summary>
        public void Dispose()
        {
            _isDecoderRunning = false;
            var isDecoderStopped = true;
            if (_decoderThread != null && _decoderThread != Thread.CurrentThread)
                isDecoderStopped = _decoderThread.Join(_decoderJoinTimeoutMs);
            _decoderThread = null;

            // A decoder still busy with a frame disposes the stream itself when it ends.
            if (isDecoderStopped)
                DisposeStream();
            _video = null;
        }

        private void DisposeStream()
        {
            // Called by the main thread or the decoder thread - whoever comes last finds null.
            Interlocked.Exchange(ref _stream, null)?.Dispose();
        }
    }
}
