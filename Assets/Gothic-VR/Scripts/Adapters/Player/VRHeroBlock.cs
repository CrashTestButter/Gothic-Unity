#if GOTHIC_HVR_INSTALLED
using Gothic.Core;
using Gothic.Core.Adapters.Vob;
using Gothic.Core.Extensions;
using Gothic.Core.Logging;
using Gothic.Core.Models.Container;
using Gothic.Core.Models.Vm;
using Gothic.Core.Services;
using Gothic.Core.Services.Caches;
using Gothic.Core.Services.Config;
using Gothic.VR.Services;
using HurricaneVR.Framework.Shared;
using Reflex.Attributes;
using UnityEngine;
using ZenKit.Daedalus;
using Logger = Gothic.Core.Logging.Logger;

namespace Gothic.VR.Adapters.Player
{
    /// <summary>
    /// DeveloperConfig.EnableVrHeroBlock: Gothic's parry in VR. Its animation holds the weapon with both hands (one hand
    /// at the grip, one at the blade) for one- and two-handed weapons alike - so: both hands on the same melee weapon and
    /// both triggers held. While it lasts the hero's props say so (IsBlocking, the weapon's material, the head's
    /// direction); FightService blocks the NPC hits from the front and ignores the hero's own hits.
    /// </summary>
    public class VRHeroBlock : MonoBehaviour
    {
        [Inject] private readonly VRPlayerService _vrPlayerService;
        [Inject] private readonly VrHapticsService _hapticsService;
        [Inject] private readonly VmCacheService _vmCacheService;
        [Inject] private readonly GameStateService _gameStateService;
        [Inject] private readonly ConfigService _configService;

        private bool _isBlocking;

        private void Awake()
        {
            this.Inject();
            GlobalEventDispatcher.HeroBlockedHit.AddListener(OnHeroBlockedHit);
        }

        private void OnDestroy()
        {
            GlobalEventDispatcher.HeroBlockedHit.RemoveListener(OnHeroBlockedHit);
        }

        private void Update()
        {
            var hero = (_gameStateService.GothicVm?.GlobalHero as NpcInstance)?.GetUserData();
            if (hero?.Props == null)
                return;

            var material = -1;
            var isBlocking = _configService.Dev.EnableVrHeroBlock && TryGetBlockingWeapon(out material) &&
                             HVRController.GetButtonState(HVRHandSide.Left, HVRButtons.Trigger).Active &&
                             HVRController.GetButtonState(HVRHandSide.Right, HVRButtons.Trigger).Active;

            hero.Props.IsBlocking = isBlocking;
            hero.Props.BlockWeaponMaterial = material;
            var head = Camera.main;
            if (head != null)
                hero.Props.BlockForward = head.transform.forward;

            if (isBlocking == _isBlocking)
                return;

            _isBlocking = isBlocking;
            Logger.Log($"[HeroBlock] {(isBlocking ? "blocks" : "stops blocking")}", LogCat.Fight);
            if (isBlocking)
            {
                _hapticsService.Vibrate(HVRHandSide.Left, VrHapticsService.VibrationType.Info);
                _hapticsService.Vibrate(HVRHandSide.Right, VrHapticsService.VibrationType.Info);
            }
        }

        /// <summary>
        /// Both hands hold the same melee weapon (ITEM_KAT_NF).
        /// </summary>
        private bool TryGetBlockingWeapon(out int material)
        {
            material = -1;
            var held = _vrPlayerService.GrabbedItemLeft;
            if (held == null || held != _vrPlayerService.GrabbedItemRight)
                return false;

            var instanceName = held.GetComponentInParent<VobLoader>()?.Container?.Vob?.Name;
            var item = instanceName != null ? _vmCacheService.TryGetItemData(instanceName) : null;
            if (item == null || (VmGothicEnums.ItemFlags)item.MainFlag != VmGothicEnums.ItemFlags.ItemKatNf)
                return false;

            material = item.Material;
            return true;
        }

        private void OnHeroBlockedHit(NpcContainer attacker)
        {
            _hapticsService.Vibrate(HVRHandSide.Left, VrHapticsService.VibrationType.Warning);
            _hapticsService.Vibrate(HVRHandSide.Right, VrHapticsService.VibrationType.Warning);
        }
    }
}
#endif
