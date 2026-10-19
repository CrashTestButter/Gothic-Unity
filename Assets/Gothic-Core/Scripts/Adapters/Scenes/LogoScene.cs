using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gothic.Core.Adapters.Video;
using Gothic.Core.Const;
using Gothic.Core.Extensions;
using Gothic.Core.Logging;
using Gothic.Core.Services;
using Gothic.Core.Services.Config;
using Gothic.Core.Services.Context;
using Gothic.Core.Services.Player;
using Reflex.Attributes;
using UnityEngine;
using Logger = Gothic.Core.Logging.Logger;

namespace Gothic.Core.Adapters.Scenes
{
    public class LogoScene : MonoBehaviour, IScene
    {
        [Inject] private readonly ConfigService _configService;
        [Inject] private readonly VideoService _videoService;
        [Inject] private readonly BootstrapService _bootstrapService;
        [Inject] private readonly ContextInteractionService _contextInteractionService;

        [SerializeField] private MeshRenderer _screen;
        [SerializeField] private AudioSource _audioSource;

        private Queue<string> _logoVideos = new();
        private BinkPlayer _binkPlayer;

        public void Init()
        {
            Logger.Log($"INI: playLogoVideos = {_configService.Gothic.IniPlayLogoVideos}", LogCat.Loading);
            if (!_configService.Gothic.IniPlayLogoVideos)
            {
                _bootstrapService.LoadScene(Constants.SceneMainMenu, Constants.SceneLogo);
                return;
            }

            _contextInteractionService.TeleportPlayerTo(Vector3.zero);

            _logoVideos = new Queue<string>(_videoService.VideoFilePathsBik
                .Where(i => Path.GetFileName(i).StartsWithIgnoreCase("logo")));

            if (_logoVideos.IsEmpty())
            {
                Logger.Log("No logo videos found. Skipping scene.", LogCat.Loading);
            }

            // Start first logo
            LoadNextLogo();
        }

        private void LoadNextLogo()
        {
            if (_binkPlayer != null)
                Destroy(_binkPlayer);

            if (_logoVideos.IsEmpty())
            {
                _bootstrapService.LoadScene(Constants.SceneMainMenu, Constants.SceneLogo);
                return;
            }

            _binkPlayer = gameObject.AddComponent<BinkPlayer>();
            if (!_binkPlayer.Play(_logoVideos.Dequeue(), _screen, _audioSource, LoadNextLogo, out var aspect))
            {
                LoadNextLogo();
                return;
            }

            var screenWidth = _screen.transform.localScale.x;
            _screen.transform.localScale = new Vector3(screenWidth, screenWidth / aspect, 1f);
        }
    }
}
