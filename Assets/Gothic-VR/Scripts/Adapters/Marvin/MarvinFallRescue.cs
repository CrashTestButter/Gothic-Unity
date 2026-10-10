#if GOTHIC_HVR_INSTALLED
using System.Linq;
using Gothic.Core.Extensions;
using Gothic.Core.Logging;
using Gothic.Core.Services;
using Gothic.Core.Services.Config;
using Gothic.Core.Services.Context;
using Reflex.Attributes;
using UnityEngine;
using Logger = Gothic.Core.Logging.Logger;

namespace Gothic.VR.Adapters.Marvin
{
    /// <summary>
    /// DeveloperConfig.EnableMarvinFallRescue (Marvin mode only): fell out of the world (e.g. after Marvin's "TP +4 m"
    /// through a wall) - no floor below the hero for a while - back to the world's start point (START / START_GOTHIC2,
    /// like entering the world).
    /// </summary>
    public class MarvinFallRescue : MonoBehaviour
    {
        [Inject] private readonly ConfigService _configService;
        [Inject] private readonly ContextInteractionService _contextInteractionService;
        [Inject] private readonly GameStateService _gameStateService;

        private const float _noFloorSeconds = 2f;
        private const float _floorSearchDistance = 200f;
        private float _noFloorTime;

        private void Awake() => this.Inject();

        private void Update()
        {
            if (!_configService.Dev.ActivateMarvinMode || !_configService.Dev.EnableMarvinFallRescue)
                return;

            var player = _contextInteractionService.GetCurrentPlayerController();
            if (player == null)
                return;

            var feet = player.transform.position;
            if (Physics.Raycast(feet + Vector3.up * 0.5f, Vector3.down, _floorSearchDistance, Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore))
            {
                _noFloorTime = 0f;
                return;
            }

            _noFloorTime += Time.unscaledDeltaTime;
            if (_noFloorTime < _noFloorSeconds)
                return;

            _noFloorTime = 0f;
            var start = _gameStateService.FreePoints.FirstOrDefault(fp =>
                fp.Key.EqualsIgnoreCase("START") || fp.Key.EqualsIgnoreCase("START_GOTHIC2"));
            if (start.Value == null)
                start = _gameStateService.FreePoints.FirstOrDefault(fp =>
                    fp.Key.StartsWith("START", System.StringComparison.OrdinalIgnoreCase));
            if (start.Value == null)
            {
                Logger.LogWarning("[MarvinFallRescue] Fell out of the world, but the world has no START point.", LogCat.Ui);
                return;
            }

            Logger.Log($"[MarvinFallRescue] No floor below the hero for {_noFloorSeconds:F0} s at {feet} - back to " +
                       $"'{start.Key}'", LogCat.Ui);
            _contextInteractionService.TeleportPlayerTo(start.Value.Position, start.Value.Rotation);
        }
    }
}
#endif
