using System.Collections.Generic;
using Gothic.VR.Interaction.Components;

namespace Gothic.VR.Interaction.Ports
{
    /// <summary>
    /// A group of sockets (backpack inventory, chest content).
    /// </summary>
    public interface IVrSocketContainer
    {
        IReadOnlyList<IVrSocket> Sockets { get; }

        /// <summary>
        /// Finds the first free socket which accepts the interactable.
        /// </summary>
        bool TryFindFree(VrInteractable interactable, out IVrSocket socket);

        /// <summary>
        /// Places the interactable into the first free socket which accepts it (with sound).
        /// </summary>
        bool TryAdd(VrInteractable interactable);
    }
}
