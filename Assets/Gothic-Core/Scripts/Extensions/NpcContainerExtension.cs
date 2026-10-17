using Gothic.Core.Models.Container;

namespace Gothic.Core.Extensions
{
    public static class NpcContainerExtension
    {
        /// <summary>
        /// The NPC reached its mob and is in one of its states (e.g. seated on a bench).
        /// </summary>
        public static bool IsUsingMob(this NpcContainer npc)
        {
            return npc.PrefabProps?.CurrentInteractable != null && npc.Props.CurrentInteractableStateId >= 0;
        }
    }
}
