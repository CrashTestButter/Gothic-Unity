using System;
using System.IO;
using Gothic.Core.Domain.Video;
using Gothic.Core.Logging;
using UnityEngine;
using Logger = Gothic.Core.Logging.Logger;

namespace Gothic.Core.Adapters.Video
{
    /// <summary>
    /// Plays an original Gothic Bink video (.bik) without any conversion: BinkPlaybackDomain decodes raw YUV frames +
    /// PCM audio, this uploads the 3 planes into R8 textures ("Gothic/Bink YUV" converts to RGB on the GPU) and streams
    /// the audio into a streaming AudioClip.
    /// </summary>
    public class BinkPlayer : MonoBehaviour
    {
        private BinkPlaybackDomain _playback;
        private Action _onFinished;
        private AudioSource _audioSource;

        private readonly Texture2D[] _textures = new Texture2D[3];
        private Material _material;
        private AudioClip _clip;

        private bool _isFinished;

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
                _playback = new BinkPlaybackDomain(path, audioSource != null);
            }
            catch (Exception e)
            {
                Logger.LogWarning($"[BinkPlayer] Can't open '{path}': {e.Message}", LogCat.Loading);
                Close();
                return false;
            }

            aspect = (float)_playback.Width / _playback.Height;
            CreateTextures(screen);
            var audioOutputLatency = SetupAudio();

            var audioInfo = _playback.HasAudio ? $"{_playback.SampleRate} Hz x{_playback.Channels}" : "none";
            Logger.Log($"[BinkPlayer] {Path.GetFileName(path)}: {_playback.Width}x{_playback.Height} " +
                       $"@ {_playback.Fps:F2} fps, {_playback.FrameCount} frames, audio={audioInfo}", LogCat.Loading);

            _playback.Start(Time.unscaledTime, audioOutputLatency);
            return true;
        }

        public void Stop()
        {
            _isFinished = true;
            Close();
        }

        private void CreateTextures(Renderer screen)
        {
            var width = _playback.Width;
            var height = _playback.Height;
            var lumaStride = (width + 15) / 16 * 16;
            var lumaHeight = (height + 15) / 16 * 16;
            var chromaStride = (width / 2 + 15) / 16 * 16;
            var chromaHeight = (height / 2 + 15) / 16 * 16;

            _textures[0] = CreatePlaneTexture(lumaStride, lumaHeight);
            _textures[1] = CreatePlaneTexture(chromaStride, chromaHeight);
            _textures[2] = CreatePlaneTexture(chromaStride, chromaHeight);

            _material = new Material(Shader.Find("Gothic/Bink YUV"));
            _material.SetTexture("_TexY", _textures[0]);
            _material.SetTexture("_TexU", _textures[1]);
            _material.SetTexture("_TexV", _textures[2]);
            _material.SetVector("_ScaleY", new Vector4((float)width / lumaStride, (float)height / lumaHeight));
            _material.SetVector("_ScaleUV",
                new Vector4((float)(width / 2) / chromaStride, (float)(height / 2) / chromaHeight));
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

        /// <summary>
        /// Returns the audio output latency (seconds from AudioSource.timeSamples until the sample is heard).
        /// </summary>
        private double SetupAudio()
        {
            if (!_playback.HasAudio)
                return 0;

            _clip = AudioClip.Create("Bink", _playback.AudioLengthFrames, _playback.Channels, _playback.SampleRate,
                true, _playback.ReadAudio);
            _audioSource.clip = _clip;
            _audioSource.loop = false;

            AudioSettings.GetDSPBufferSize(out var bufferLength, out var bufferCount);
            return (double)bufferLength * bufferCount / AudioSettings.outputSampleRate;
        }

        private void Update()
        {
            if (_playback == null || _isFinished)
                return;

            if (_playback.DecodeError != null)
            {
                Logger.LogWarning($"[BinkPlayer] Decoding stopped: {_playback.DecodeError}", LogCat.Loading);
                Finish();
                return;
            }

            if (_playback.TryStartAudio(Time.unscaledTime))
                _audioSource.Play();

            long? audioPlayHead = _audioSource != null && _audioSource.isPlaying ? _audioSource.timeSamples : null;
            var frame = _playback.Update(Time.unscaledTime, Time.unscaledDeltaTime, audioPlayHead);
            if (frame != null)
            {
                for (var plane = 0; plane < 3; plane++)
                {
                    _textures[plane].LoadRawTextureData(frame.Planes[plane]);
                    _textures[plane].Apply(false);
                }

                _playback.ReleaseFrame(frame);
            }

            if (_playback.IsFinished)
                Finish();
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
            _playback?.Dispose();
            _playback = null;

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

        private void OnDestroy()
        {
            Close();
        }
    }
}
