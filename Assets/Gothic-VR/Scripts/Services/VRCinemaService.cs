#if GOTHIC_HVR_INSTALLED
using Gothic.Core;
using Gothic.Core.Logging;
using Gothic.Core.Services.Player;
using Gothic.VR.Adapters.UI;
using Reflex.Attributes;
using UnityEngine;
using Logger = Gothic.Core.Logging.Logger;

namespace Gothic.VR.Services
{
    /// <summary>
    /// Plays the videos the scripts start with PlayVideo/PlayVideoEx in a VRCinema. Keeps the one cinema that is open,
    /// so several calls in a row (G1 ending: Extro + Credits) are queued instead of opening several cinemas.
    /// </summary>
    public class VRCinemaService
    {
        [Inject] private readonly VideoService _videoService;

        private VRCinema _cinema;

        public void Init()
        {
            GlobalEventDispatcher.ScriptPlayVideo.AddListener(Play);
        }

        public void Play(string fileName)
        {
            var path = _videoService.FindVideo(fileName);
            if (path == null)
            {
                Logger.LogWarning($"[VRCinema] '{fileName}' not found in _work/DATA/video/ - skipped.",
                    LogCat.VR);
                return;
            }

            // The cinema destroys itself after its last video - Unity's null check then creates a new one.
            if (_cinema == null)
                _cinema = new GameObject("_Cinema").AddComponent<VRCinema>();

            _cinema.Enqueue(path);
        }
    }
}
#endif
