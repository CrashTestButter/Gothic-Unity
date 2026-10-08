#if GOTHIC_HVR_INSTALLED
using Gothic.Core.Adapters.Npc;
using Gothic.Core.Manager;
using Gothic.Core.Models.Container;
using Gothic.Core.Models.Vm;
using Gothic.Core.Services;
using Gothic.Core.Services.Config;
using Gothic.Core.Services.Npc;
using Gothic.Core.Services.World;
using Gothic.Core;
using Gothic.Core.Extensions;
using HurricaneVR.Framework.Core;
using HurricaneVR.Framework.Core.Grabbers;
using Reflex.Attributes;
using UnityEngine;
using ZenKit.Daedalus;

namespace Gothic.VR.Adapters
{
    public class VRNpc : MonoBehaviour
    {
        [Inject] private readonly GameStateService _gameStateService;
        [Inject] private readonly DialogService _dialogService;
        [Inject] private readonly NpcAiService _npcAiService;
        [Inject] private readonly ConfigService _configService;
        [Inject] private readonly PhysicsService _physicsService;

        private NpcContainer _npcData;
        private VRNpcLoot _npcLoot;

        private void Awake()
        {
            _npcData = GetComponentInParent<NpcLoader>().Npc.GetUserData();
            _npcLoot = GetComponent<VRNpcLoot>();
        }

        public void OnGrabbed(HVRGrabberBase grabber, HVRGrabbable grabbable)
        {
            var isDead = _npcData.Props.BodyState == VmGothicEnums.BodyState.BsDead;

            if (isDead && _configService.Dev.EnableNpcLooting && _npcLoot != null)
            {
                _npcLoot.Toggle(_npcData);
                // HVR sets isKinematic=false during grab; release immediately and freeze the corpse
                grabber.ForceRelease();
                _physicsService.DisablePhysicsForNpc(_npcData.PrefabProps);
                return;
            }

            if (_gameStateService.Dialogs.IsInDialog)
            {
                _dialogService.SkipCurrentDialogLine(_npcData.Props);
            }
            else
            {
                _gameStateService.Dialogs.WasPlayerInitiated = true;
                _npcAiService.ExecutePerception(VmGothicEnums.PerceptionType.AssessTalk, _npcData.Props, _npcData.Instance, null, (NpcInstance)_gameStateService.GothicVm.GlobalHero);
            }

            // Talking/skipping is a "click" - don't keep holding the NPC. While held, HVR drives its rigidbody
            // (kinematic velocity errors) and turned physics back on: seated NPCs got pushed up out of the bench.
            grabber.ForceRelease();
            if (_npcData.PrefabProps.CurrentInteractable != null && _npcData.Props.CurrentInteractableStateId >= 0)
                _physicsService.DisablePhysicsForNpc(_npcData.PrefabProps);
        }
    }
}
#endif
