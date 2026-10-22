using Gothic.Core.Extensions;
using Gothic.Core.Logging;
using Gothic.Core.Models.Container;
using Gothic.Core.Models.Vm;
using Gothic.Core.Services.Npc;
using UnityEngine;
using ZenKit.Daedalus;
using Logger = Gothic.Core.Logging.Logger;

namespace Gothic.Core.Domain.Npc.Actions.AnimationActions
{
    /// <summary>
    /// DeveloperConfig.EnableEngineFinishingMove: AI_FinishingMove like the engine (OpenGothic Npc::finishingMove):
    /// queued after walking to the unconscious target (NPC_ATTACK_FINISH_DISTANCE), turning and drawing a melee weapon -
    /// t_1hSFinish / t_2hSFinish is played and the target dies with the stab (the animation's hit frame, else its end).
    /// Not unconscious any more (stood up, already dead) or no melee weapon: nothing happens.
    /// </summary>
    public class FinishingMove : AbstractAnimationAction
    {
        private NpcContainer _target => Action.Instance0?.GetUserData();
        private string _animationName;
        private bool _hasKilled;

        public FinishingMove(AnimationAction action, NpcContainer npcContainer) : base(action, npcContainer)
        {
        }

        public override void Start()
        {
            var target = _target;
            var weaponState = (VmGothicEnums.WeaponState)Vob.FightMode;
            if (target?.Go == null || !NpcHelperService.IsKnockedOut(target) ||
                weaponState is not (VmGothicEnums.WeaponState.W1H or VmGothicEnums.WeaponState.W2H))
            {
                Logger.Log($"[FinishingMove] {NpcInstance.GetName(NpcNameSlot.Slot0)}: target not unconscious or no " +
                           $"melee weapon ({weaponState}) - no finishing move", LogCat.Fight);
                IsFinishedFlag = true;
                return;
            }

            var toTarget = target.Go.transform.position - NpcGo.transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude > 0.001f)
                NpcGo.transform.rotation = Quaternion.LookRotation(toTarget);

            _animationName = AnimationService.GetAnimationName(VmGothicEnums.AnimationType.AttackFinish, NpcContainer);
            if (string.IsNullOrEmpty(_animationName) || !PrefabProps.AnimationSystem.PlayAnimation(_animationName))
            {
                // No finishing animation (monster model): the stab without one, like the engine's state change.
                Kill();
                IsFinishedFlag = true;
                return;
            }
            ActionEndEventTime = PrefabProps.AnimationSystem.GetAnimationDuration(_animationName);
            Logger.Log($"[FinishingMove] {NpcInstance.GetName(NpcNameSlot.Slot0)} finishes " +
                       $"{target.Instance.GetName(NpcNameSlot.Slot0)} ({_animationName})", LogCat.Fight);
        }

        public override void Tick()
        {
            base.Tick();

            if (!_hasKilled && (IsFinishedFlag ||
                                PrefabProps.AnimationSystem.HasOptimalFrameReached(_animationName)))
                Kill();
        }

        private void Kill()
        {
            if (_hasKilled)
                return;
            _hasKilled = true;

            var target = _target;
            if (target == null || !NpcHelperService.IsKnockedOut(target))
                return;
            GlobalEventDispatcher.FightFinishingMove.Invoke(NpcContainer, target);
        }
    }
}
