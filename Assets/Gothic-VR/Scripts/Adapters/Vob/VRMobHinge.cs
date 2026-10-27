using Gothic.Core.Adapters.Properties.Vobs;
using Gothic.VR.Interaction.Ports;

namespace Gothic.VR.Adapters.Vob
{
    /// <summary>
    /// The hinge of a door/chest lid follows the mob's current state (InteractiveProperties.State, saved by
    /// MobStatesSaveSection): it's created open in state 1, and opening/closing it by hand sets the state.
    /// </summary>
    public static class VRMobHinge
    {
        public static void Follow(IVrHinge hinge, InteractiveProperties mob)
        {
            if (hinge == null || mob == null)
                return;

            if (mob.State > 0)
                hinge.OpenInstantly();

            hinge.Opened += () => mob.State = 1;
            hinge.Closed += () => mob.State = 0;
        }
    }
}
