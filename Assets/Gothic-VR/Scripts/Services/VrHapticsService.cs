#if GOTHIC_HVR_INSTALLED
using Gothic.Core;
using Gothic.Core.Models.Container;
using Gothic.VR.Interaction.Backends.Hvr;
using Gothic.VR.Interaction.Models;
using HurricaneVR.Framework.Shared;
using Reflex.Attributes;

namespace Gothic.VR.Services
{
    public class VrHapticsService
    {
        [Inject] private readonly VRPlayerService _vrPlayerService;
        [Inject] private readonly VRWeaponService _vrWeaponService;

        // Indexed by VrHapticPreset.
        private readonly HapticData[] _vibrationData =
        {
            // Info - e.g., you are inside an interaction collider
            new (0.15f, 0.25f, 30f),

            // Warning - e.g., your lock pick rotation is invalid
            new (0.25f, 0.5f, 100f),

            // Error - e.g., your key is broken
            new (0.4f, 0.8f, 175f),

            // Success - e.g., you opened a chest
            new (0.5f, 0.6f, 80f)
        };

        public VrHapticsService()
        {
            // Fight
            GlobalEventDispatcher.FightWindowAttack.AddListener(combatant => AttackVibration(combatant, VrHapticPreset.Success)); // Attack can happen now
            GlobalEventDispatcher.FightWindowCombo.AddListener(combatant => AttackVibration(combatant, VrHapticPreset.Info)); // Now you can start another attack
            GlobalEventDispatcher.FightWindowComboFailed.AddListener(combatant => AttackVibration(combatant, VrHapticPreset.Error)); // Attack window passed

            // Lock picking
            GlobalEventDispatcher.LockPickComboWrong.AddListener((_, _, handSide) => Vibrate((VrHandSide)handSide, VrHapticPreset.Warning));
            GlobalEventDispatcher.LockPickComboBroken.AddListener((_, _, handSide) => Vibrate((VrHandSide)handSide, VrHapticPreset.Error));
            GlobalEventDispatcher.LockPickComboCorrect.AddListener((_, _, handSide) => Vibrate((VrHandSide)handSide, VrHapticPreset.Success));
            GlobalEventDispatcher.LockPickComboFinished.AddListener((_, _, handSide) => Vibrate((VrHandSide)handSide, VrHapticPreset.Success));
        }

        private void AttackVibration(NpcContainer combatant, VrHapticPreset preset)
        {
            // Resolve which hand holds the weapon for this combatant.
            // For NPCs attacking the player, we don't vibrate — only when it's the player's own attack.
            var handSide = _vrWeaponService.GetHandSideForCombatant(combatant);

            if (handSide == GlobalEventDispatcher.HandSide.Both)
            {
                Vibrate(VrHandSide.Left, preset);
                Vibrate(VrHandSide.Right, preset);
            }
            else if (handSide.TryToVrHandSide(out var vrHandSide))
            {
                Vibrate(vrHandSide, preset);
            }
        }


        public void Vibrate(VrHandSide handSide, VrHapticPreset preset)
        {
            _vrPlayerService.GetHand(handSide.ToHvr()).Vibrate(_vibrationData[(int)preset]);
        }
    }
}
#endif
