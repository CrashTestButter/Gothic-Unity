using System.Collections.Generic;
using UnityEngine;

namespace Gothic.VR.Interaction.Ports
{
    /// <summary>
    /// Implemented by the backend bridge next to a VrInteractable. Game code uses VrInteractable instead.
    /// The bridge also raises the VrInteractable events.
    /// </summary>
    public interface IVrInteractableBackend
    {
        bool IsHeld { get; }
        IReadOnlyList<Collider> Colliders { get; }
        void ForceRelease();
        void SetupColliders();
        void SetSocketSounds(AudioClip socketed, AudioClip unsocketed);
    }
}
