using System.Collections.Generic;
using UnityEngine;

namespace Gothic.VR.Interaction.Ports
{
    /// <summary>
    /// Implemented by the backend bridge next to a VrInteractable. Game code uses VrInteractable instead.
    /// The bridge also raises the VrInteractable events.
    /// Prefabs carry the bridges of all backends; only the bridge of the active backend is used.
    /// </summary>
    public interface IVrInteractableBackend
    {
        /// <summary>
        /// Whether this bridge belongs to the active VR backend.
        /// </summary>
        bool IsActive { get; }

        bool IsHeld { get; }
        IReadOnlyList<Collider> Colliders { get; }
        void ForceRelease();
        void SetupColliders();
        void SetSocketSounds(AudioClip socketed, AudioClip unsocketed);
    }
}
