using System.IO;
using System.Linq;
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
    /// <summary>
    /// Plays the logo videos in the same cinema as the in-game videos (GlobalEventDispatcher.PlayVideo): black room,
    /// same screen size, player locked, skippable. Loads the main menu afterwards.
    /// </summary>
    public class LogoScene : MonoBehaviour, IScene
    {
        [Inject] private readonly ConfigService _configService;
        [Inject] private readonly VideoService _videoService;
        [Inject] private readonly BootstrapService _bootstrapService;
        [Inject] private readonly ContextInteractionService _contextInteractionService;


        public void Init()
        {
            Logger.Log($"INI: playLogoVideos = {_configService.Gothic.IniPlayLogoVideos}", LogCat.Loading);
            if (!_configService.Gothic.IniPlayLogoVideos)
            {
                LoadMainMenu();
                return;
            }

            var logoVideos = _videoService.VideoFilePathsBik
                .Where(i => Path.GetFileName(i).StartsWithIgnoreCase("logo"))
                .ToList();

            if (logoVideos.IsEmpty())
            {
                Logger.Log("No logo videos found. Skipping scene.", LogCat.Loading);
                LoadMainMenu();
                return;
            }

            _contextInteractionService.TeleportPlayerTo(Vector3.zero);

            GlobalEventDispatcher.VideoPlaybackFinished.AddListener(OnVideoPlaybackFinished);
            logoVideos.ForEach(GlobalEventDispatcher.PlayVideo.Invoke);
        }

        private void OnVideoPlaybackFinished()
        {
            GlobalEventDispatcher.VideoPlaybackFinished.RemoveListener(OnVideoPlaybackFinished);
            LoadMainMenu();
        }

        private void OnDestroy()
        {
            GlobalEventDispatcher.VideoPlaybackFinished.RemoveListener(OnVideoPlaybackFinished);
        }

        private void LoadMainMenu()
        {
            _bootstrapService.LoadScene(Constants.SceneMainMenu, Constants.SceneLogo);
        }
    }
}
