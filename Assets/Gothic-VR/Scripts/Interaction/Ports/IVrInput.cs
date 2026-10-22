using Gothic.VR.Interaction.Models;
using UnityEngine;

namespace Gothic.VR.Interaction.Ports
{
    /// <summary>
    /// Raw controller input of the VR player. Gothic-specific inputs (menu, both grips) are derived from it in
    /// VRPlayer.
    /// </summary>
    public interface IVrInput
    {
        /// <summary>
        /// The player is controlled by keyboard and mouse instead of controllers.
        /// </summary>
        bool IsSimulator { get; }

        VrButtonState GetButton(VrHandSide side, VrButton button);

        /// <summary>
        /// Velocity of the tracked controller, relative to IVrRig.TrackingSpace.
        /// </summary>
        Vector3 GetControllerVelocity(VrHandSide side);

        /// <summary>
        /// Activates the desktop device simulator (DeveloperConfig.EnableVRDeviceSimulator).
        /// </summary>
        void SetDeviceSimulatorActive(bool isActive);
    }
}
