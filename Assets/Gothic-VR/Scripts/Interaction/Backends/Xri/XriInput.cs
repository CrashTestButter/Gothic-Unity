using Gothic.Core.Extensions;
using Gothic.Core.Logging;
using Gothic.VR.Interaction.Models;
using Gothic.VR.Interaction.Ports;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;
using Logger = Gothic.Core.Logging.Logger;

namespace Gothic.VR.Interaction.Backends.Xri
{
    /// <summary>
    /// Controller input from GothicVR.inputactions (maps "Gothic Left", "Gothic Right" and "Gothic").
    /// The XRI maps of the same asset are enabled by the XRI rig's components, not here.
    /// </summary>
    public class XriInput : IVrInput
    {
        public const string InputActionsResourcePath = "VR/Input/GothicVR";

        private const string _leftHandMapName = "Gothic Left";
        private const string _rightHandMapName = "Gothic Right";
        private const string _gothicMapName = "Gothic";
        private const string _velocityActionName = "Velocity";
        private const string _hapticActionName = "Haptic";

        // Indexed by VrButton.
        private static readonly string[] _buttonActionNames =
        {
            "Grip", "Trigger", "Primary", "Secondary", "Menu", "Joystick Button"
        };

        // Indexed by XriGothicAction.
        private static readonly string[] _gothicActionNames = { "Menu", "Jump", "Crouch", "Sprint" };

        private readonly InputAction[,] _buttons = new InputAction[2, _buttonActionNames.Length];
        private readonly InputAction[] _velocities = new InputAction[2];
        private readonly InputAction[] _haptics = new InputAction[2];
        private readonly InputAction[] _gothicActions = new InputAction[_gothicActionNames.Length];

        private XRInteractionSimulator _simulator;
        private bool _isSimulatorActive;

        public InputActionAsset Actions { get; }

        public bool IsSimulator => _isSimulatorActive && _simulator != null && _simulator.isActiveAndEnabled;

        public XriInput()
        {
            Actions = Resources.Load<InputActionAsset>(InputActionsResourcePath);
            if (Actions == null)
            {
                Logger.LogError($"XRI input actions not found at Resources/{InputActionsResourcePath}. " +
                                "VR controller input won't work.", LogCat.VR);
                return;
            }

            InitHand(VrHandSide.Left, _leftHandMapName);
            InitHand(VrHandSide.Right, _rightHandMapName);

            var gothicMap = Actions.FindActionMap(_gothicMapName, true);
            for (var i = 0; i < _gothicActionNames.Length; i++)
                _gothicActions[i] = gothicMap.FindAction(_gothicActionNames[i], true);
            gothicMap.Enable();
        }

        private void InitHand(VrHandSide side, string mapName)
        {
            var map = Actions.FindActionMap(mapName, true);
            for (var i = 0; i < _buttonActionNames.Length; i++)
                _buttons[(int)side, i] = map.FindAction(_buttonActionNames[i], true);

            _velocities[(int)side] = map.FindAction(_velocityActionName, true);
            _haptics[(int)side] = map.FindAction(_hapticActionName, true);
            map.Enable();
        }

        public VrButtonState GetButton(VrHandSide side, VrButton button)
        {
            return ToState(_buttons[(int)side, (int)button]);
        }

        /// <summary>
        /// Gothic-specific actions (jump, crouch, sprint, menu) incl. their simulator keys.
        /// </summary>
        public VrButtonState GetGothicAction(XriGothicAction action)
        {
            return ToState(_gothicActions[(int)action]);
        }

        public Vector3 GetControllerVelocity(VrHandSide side)
        {
            var action = _velocities[(int)side];
            return action == null ? Vector3.zero : action.ReadValue<Vector3>();
        }

        /// <summary>
        /// Pass-through action bound to the controller device, used to send haptic impulses.
        /// </summary>
        public InputAction GetHapticAction(VrHandSide side)
        {
            return _haptics[(int)side];
        }

        public void SetDeviceSimulatorActive(bool isActive)
        {
            _isSimulatorActive = isActive;

            if (_simulator == null)
                _simulator = SceneManager.GetActiveScene().GetComponentInChildren<XRInteractionSimulator>(true);

            if (_simulator == null)
            {
                if (isActive)
                    Logger.LogWarning("XRI backend: no XR Interaction Simulator in the active scene. " +
                                      "It's added to the player scene with HVR phase 1d.", LogCat.VR);
                return;
            }

            _simulator.gameObject.SetActive(isActive);
        }

        private static VrButtonState ToState(InputAction action)
        {
            if (action == null)
                return default;

            return new VrButtonState(action.IsPressed(), action.WasPressedThisFrame(), action.WasReleasedThisFrame());
        }
    }
}
