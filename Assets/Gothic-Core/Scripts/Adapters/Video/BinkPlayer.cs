using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Gothic.Core.Bink;
using Gothic.Core.Logging;
using UnityEngine;
using Logger = Gothic.Core.Logging.Logger;

namespace Gothic.Core.Adapters.Video
{
    /// <summary>
    /// Plays an original Gothic Bink video (.bik) without any conversion: BinkVideo decodes raw YUV frames + PCM
    /// audio, this uploads the 3 planes into R8 textures ("Gothic/Bink YUV" converts to RGB on the GPU) and streams
    /// the audio into a streaming AudioClip. The picture follows the audio clock (or real time if silent).
    /// Decoding runs on its own thread - in the editor (Mono, debug) a frame took longer than 40 ms on the main thread.
    /// </summary>
    public class BinkPlayer : MonoBehaviour
    {
        private const int _framesAhead = 12;
        private const float _ringSeconds = 4f;
        private const int _decoderIdleMs = 2;
        private const int _decoderJoinTimeoutMs = 500;

        private class DecodedFrame
        {
            public int Index;
            // Position (sample frames) in the audio stream where this frame's audio starts - the frame is shown when
            // that sample is heard. Exact even if a frame's audio packet isn't 1/fps long (e.g. pre-roll).
            public long AudioStart;
            public readonly byte[][] Planes = new byte[3][];
        }

        private Stream _stream;
        private BinkVideo _video;
        private Action _onFinished;
        private AudioSource _audioSource;

        private readonly Texture2D[] _textures = new Texture2D[3];
        private Material _material;
        private AudioClip _clip;
        // Shared with the decoder thread - always under _frameLock.
        private readonly object _frameLock = new();
        private readonly Queue<DecodedFrame> _decoded = new();
        private readonly Stack<DecodedFrame> _pool = new();
        private float _startTime;

        // The video clock runs on real time and is pulled gently towards the audio play head (AudioSource.timeSamples).
        // Counting the samples Unity pulled from OnAudioRead was ~280 ms ahead and jumpy (it reads big chunks ahead):
        // the clock re-synced every second and skipped 12-18 frames per second. Hard re-sync only after a real hiccup.
        private const double _clockResyncSeconds = 1.0;
        private const double _clockCorrectionPerSecond = 0.5;
        private double _videoClock;
        // timeSamples is the position handed to the mixer - it's heard after the DSP buffers. The picture waits for it
        // (video was slightly ahead of the sound).
        private double _audioOutputLatency;

        private bool _isFinished;

        private Thread _decoderThread;
        private volatile bool _isDecoderRunning;
        private volatile bool _isDecodingDone;
        private volatile string _decodeError;
        private int _frameCount;
        private double _fps;

        // Audio ring buffer, read on the audio thread (OnAudioRead).
        private readonly object _audioLock = new();
        private float[] _ring;
        private int _ringRead;
        private int _ringCount;
        private long _samplesPlayed;
        private long _audioPushedFrames; // decoder thread
        private int _sampleRate;
        private int _channels;
        private bool _hasAudio;
        private bool _isAudioStarted;

        /// <summary>
        /// Starts the video on the screen renderer. onFinished is called at the end (also on decode errors).
        /// Returns false if the file can't be opened as Bink.
        /// </summary>
        public bool Play(string path, Renderer screen, AudioSource audioSource, Action onFinished, out float aspect)
        {
            aspect = 4f / 3f;
            _onFinished = onFinished;
            _audioSource = audioSource;

            try
            {
                _stream = new BufferedStream(File.OpenRead(path), 1 << 16);
                _video = new BinkVideo(_stream);
            }
            catch (Exception e)
            {
                Logger.LogWarning($"[BinkPlayer] Can't open '{path}': {e.Message}", LogCat.Loading);
                Close();
                return false;
            }

            aspect = (float)_video.Width / _video.Height;
            _frameCount = (int)_video.FrameCount;
            _fps = _video.Fps;
            CreateTextures(screen);
            SetupAudio();

            var audioInfo = _hasAudio ? $"{_sampleRate} Hz x{_channels}" : "none";
            Logger.Log($"[BinkPlayer] {Path.GetFileName(path)}: {_video.Width}x{_video.Height} " +
                       $"@ {_video.Fps:F2} fps, {_video.FrameCount} frames, audio={audioInfo}", LogCat.Loading);

            _startTime = Time.unscaledTime;
            _isDecoderRunning = true;
            _decoderThread = new Thread(DecodeLoop) { IsBackground = true, Name = "BinkDecoder" };
            _decoderThread.Start();
            return true;
        }

        public void Stop()
        {
            _isFinished = true;
            Close();
        }

        private void CreateTextures(Renderer screen)
        {
            var lumaStride = (_video.Width + 15) / 16 * 16;
            var lumaHeight = (_video.Height + 15) / 16 * 16;
            var chromaStride = (_video.Width / 2 + 15) / 16 * 16;
            var chromaHeight = (_video.Height / 2 + 15) / 16 * 16;

            _textures[0] = CreatePlaneTexture(lumaStride, lumaHeight);
            _textures[1] = CreatePlaneTexture(chromaStride, chromaHeight);
            _textures[2] = CreatePlaneTexture(chromaStride, chromaHeight);

            _material = new Material(Shader.Find("Gothic/Bink YUV"));
            _material.SetTexture("_TexY", _textures[0]);
            _material.SetTexture("_TexU", _textures[1]);
            _material.SetTexture("_TexV", _textures[2]);
            _material.SetVector("_ScaleY",
                new Vector4((float)_video.Width / lumaStride, (float)_video.Height / lumaHeight));
            _material.SetVector("_ScaleUV",
                new Vector4((float)(_video.Width / 2) / chromaStride, (float)(_video.Height / 2) / chromaHeight));
            screen.sharedMaterial = _material;
        }

        private static Texture2D CreatePlaneTexture(int width, int height)
        {
            return new Texture2D(width, height, TextureFormat.R8, false, true)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
        }

        private void SetupAudio()
        {
            if (_video.AudioTracks.Count == 0 || _audioSource == null)
                return;

            var track = _video.AudioTracks[0];
            _sampleRate = track.SampleRate;
            _channels = track.IsStereo ? 2 : 1;
            _ring = new float[(int)(_sampleRate * _channels * _ringSeconds)];
            _hasAudio = true;

            var lengthFrames = (int)Math.Ceiling(_video.FrameCount / _video.Fps * _sampleRate) + _sampleRate;
            _clip = AudioClip.Create("Bink", lengthFrames, _channels, _sampleRate, true, OnAudioRead);
            _audioSource.clip = _clip;
            _audioSource.loop = false;

            AudioSettings.GetDSPBufferSize(out var bufferLength, out var bufferCount);
            _audioOutputLatency = (double)bufferLength * bufferCount / AudioSettings.outputSampleRate;
        }

        private void Update()
        {
            if (_video == null || _isFinished)
                return;

            if (_decodeError != null)
            {
                Logger.LogWarning($"[BinkPlayer] Decoding stopped: {_decodeError}", LogCat.Loading);
                Finish();
                return;
            }

            int decodedCount;
            lock (_frameLock)
                decodedCount = _decoded.Count;

            // Start the sound once a few frames are buffered - the picture follows the sound from then on.
            if (_hasAudio && !_isAudioStarted && (decodedCount >= _framesAhead || _isDecodingDone))
            {
                _isAudioStarted = true;
                _startTime = Time.unscaledTime;
                _audioSource.Play();
            }

            UpdateVideoClock();
            ShowCurrentFrame();

            if (_isDecodingDone && decodedCount == 0 && GetPlaybackTime() * _fps >= _frameCount)
                Finish();
        }

        /// <summary>
        /// Decoder thread: keeps up to _framesAhead frames (and their audio) ready. Touches no Unity API.
        /// Works on its own reference of the video: Close() may clear _video while a frame is still decoded.
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
                        {
                            lock (_frameLock)
                                _pool.Push(decoded);
                        }
                        Thread.Sleep(_decoderIdleMs);
                        continue;
                    }

                    decoded.Index = (int)video.CurrentFrame;
                    var frame = video.NextFrame();
                    for (var plane = 0; plane < 3; plane++)
                    {
                        var source = frame.Planes[plane].Data;
                        if (decoded.Planes[plane] == null || decoded.Planes[plane].Length != source.Length)
                            decoded.Planes[plane] = new byte[source.Length];
                        Buffer.BlockCopy(source, 0, decoded.Planes[plane], 0, source.Length);
                    }

                    decoded.AudioStart = _audioPushedFrames;
                    if (_hasAudio)
                    {
                        var samples = frame.Audio[0];
                        PushAudio(samples);
                        _audioPushedFrames += samples.Count / _channels;
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
                // Close() couldn't wait for this thread (join timed out) and left the stream to it.
                if (!_isDecoderRunning)
                    DisposeStream();
            }
        }

        private bool HasAudioRoom()
        {
            if (!_hasAudio)
                return true;
            // One video frame of samples (with margin) has to fit into the ring.
            var frameSamples = (int)(_sampleRate * _channels / _fps) * 2;
            lock (_audioLock)
                return _ring.Length - _ringCount >= frameSamples;
        }

        private void ShowCurrentFrame()
        {
            if (_hasAudio && !_isAudioStarted)
                return;

            var playbackTime = GetPlaybackTime();
            var targetIndex = (int)(playbackTime * _fps);
            // With sound: the frame whose audio is being heard. Behind -> older frames are skipped, ahead -> it waits.
            var heardSample = (long)(playbackTime * _sampleRate);
            DecodedFrame toShow = null;
            lock (_frameLock)
            {
                while (_decoded.Count > 0 && (_hasAudio
                           ? _decoded.Peek().AudioStart <= heardSample
                           : _decoded.Peek().Index <= targetIndex))
                {
                    if (toShow != null)
                        _pool.Push(toShow);
                    toShow = _decoded.Dequeue();
                }
            }

            if (toShow == null)
                return;

            for (var plane = 0; plane < 3; plane++)
            {
                _textures[plane].LoadRawTextureData(toShow.Planes[plane]);
                _textures[plane].Apply(false);
            }

            lock (_frameLock)
                _pool.Push(toShow);
        }

        private double GetPlaybackTime()
        {
            return _hasAudio
                ? Math.Max(0.0, _videoClock - _audioOutputLatency)
                : Time.unscaledTime - _startTime;
        }

        private void UpdateVideoClock()
        {
            if (!_hasAudio || !_isAudioStarted)
                return;

            double audioTime;
            if (_audioSource.isPlaying)
            {
                audioTime = (double)_audioSource.timeSamples / _sampleRate;
            }
            else
            {
                lock (_audioLock)
                    audioTime = (double)_samplesPlayed / (_sampleRate * _channels);
            }

            var deltaTime = Time.unscaledDeltaTime;
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
        private void OnAudioRead(float[] data)
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

        private void Finish()
        {
            if (_isFinished)
                return;
            _isFinished = true;
            Close();
            _onFinished?.Invoke();
        }

        /// <summary>
        /// Safe to call twice (Stop, then OnDestroy).
        /// </summary>
        private void Close()
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

            if (_audioSource != null)
            {
                _audioSource.Stop();
                if (_audioSource.clip == _clip)
                    _audioSource.clip = null;
            }
            if (_clip != null)
                Destroy(_clip);
            _clip = null;

            if (_material != null)
                Destroy(_material);
            _material = null;

            for (var plane = 0; plane < _textures.Length; plane++)
            {
                if (_textures[plane] != null)
                    Destroy(_textures[plane]);
                _textures[plane] = null;
            }
        }

        private void DisposeStream()
        {
            // Called by the main thread or the decoder thread - whoever comes last finds null.
            Interlocked.Exchange(ref _stream, null)?.Dispose();
        }

        private void OnDestroy()
        {
            Close();
        }
    }
}
