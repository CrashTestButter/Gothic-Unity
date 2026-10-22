#if GOTHIC_HVR_INSTALLED
using Gothic.Core;
using Gothic.Core.Adapters.UI.Menus;
using Gothic.Core.Const;
using Gothic.Core.Services;
using Gothic.Core.Services.Config;
using Gothic.VR.Adapters.UI;
using Gothic.VR.Interaction.Backends.Hvr;
using Gothic.VR.Interaction.Models;
using HurricaneVR.Framework.Core.Player;
using MyBox;
using Reflex.Attributes;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gothic.VR.Adapters.HVROverrides
{
    public class VRPlayerController : HVRPlayerController
    {
        [Inject] private readonly ConfigService _configService;
        [Inject] private readonly GameStateService _gameStateService;

        public VRPlayerInputs VrInputs => (VRPlayerInputs)Inputs;

        [Separator("Gothic - Settings")]
        public MenuHandler MenuHandler;
        public VRCinema Cinema;

        [SerializeField] private float _characterControllerSwimDiveStepHeight = 1f; // 1m means walking out of water in Xardas' old tower in G1.

        // For resetting values when stop swimming.
        private float _defaultCharacterControllerStepHeight;

        protected override void Start()
        {
            base.Start();
            GlobalEventDispatcher.PlayerPrefUpdated.AddListener(OnPlayerPrefsUpdated);

            _defaultCharacterControllerStepHeight = CharacterController.stepOffset;

            // Enabled later via button press or other events
            MenuHandler?.gameObject.SetActive(false);
        }

        protected override void Update()
        {
            base.Update();

            if (VrInputs.IsMenuActivated && IsGameScene() && (Cinema == null || !Cinema.IsBlockingInput))
            {
                _gameStateService.InGameAndAlive = true;
                MenuHandler.ToggleVisibility();
            }
        }

        private void OnDestroy()
        {
            GlobalEventDispatcher.PlayerPrefUpdated.RemoveListener(OnPlayerPrefsUpdated);
        }

        /// <summary>
        /// Used in game scenes where you play the game (world.unity, ...)
        /// </summary>
        public void SetNormalControls(bool useDefaultValues = false)
        {
            // We have our player created before Gothic inis are loaded. We therefore need to set some default values.
            if (useDefaultValues)
            {
                ApplyComfortSettings(VrSitStand.PlayerHeight, VrMoveDirection.Camera, VrTurnType.Snap, 45f, 90f);
                return;
            }

            var gothicIni = _configService.Gothic;
            var sitStand = (VrSitStand)gothicIni.GetInt(VRConstants.IniNames.SitStand, (int)VrSitStand.PlayerHeight);
            var moveDirection =
                (VrMoveDirection)gothicIni.GetInt(VRConstants.IniNames.MoveDirection, (int)VrMoveDirection.Camera);
            var turnType = (VrTurnType)gothicIni.GetInt(VRConstants.IniNames.RotationType, (int)VrTurnType.Snap);

            var snapSetting = gothicIni.GetInt(VRConstants.IniNames.SnapRotationAmount, VRConstants.SnapRotationDefaultValue);
            // e.g., 20° = 5° + 3*5°
            var snapDegrees = VRConstants.SnapRotationAmountSettingTickAmount + VRConstants.SnapRotationAmountSettingTickAmount * snapSetting;

            var smoothSetting = gothicIni.GetFloat(VRConstants.IniNames.SmoothRotationSpeed, VRConstants.SmoothRotationDefaultValue);
            // e.g., 50 = 5 + 90 * 0.5f
            var smoothSpeed = VRConstants.SmoothRotationMinSpeed + VRConstants.SmoothRotationMaxAdditionalSpeed * smoothSetting;

            ApplyComfortSettings(sitStand, moveDirection, turnType, snapDegrees, smoothSpeed);
        }

        public void ApplyComfortSettings(VrSitStand sitStand, VrMoveDirection moveDirection, VrTurnType turnType,
            float snapDegrees, float smoothSpeed)
        {
            CameraRig.SetSitStandMode(sitStand.ToHvr());
            DirectionStyle = moveDirection.ToHvr();
            RotationType = turnType.ToHvr();
            SnapAmount = snapDegrees;
            SmoothTurnSpeed = smoothSpeed;
        }

        /// <summary>
        /// Disable certain actions to keep player stuck in current position.
        /// </summary>
        public void SetLockedControls()
        {
            // HINT: Disable physics
            // We can't disable physics as it would prevent HVRTeleport.Teleport() from finishing (as it checks for Player.IsGrounded every frame).
            // Therefore, we need to ground the player always on a plane and disable movement only.

            MovementEnabled = false;
            RotationEnabled = false;
            Teleporter.enabled = false;
        }

        public void SetUnlockedControls()
        {
            MovementEnabled = true;
            RotationEnabled = true;
            Teleporter.enabled = true;
        }
        
        public void SetWalkingControls()
        {
            ChangeGrabbing(true);

            CanCrouch = true;
            CanJump = true;
            Teleporter.enabled = true;
            CharacterController.stepOffset = _defaultCharacterControllerStepHeight;

            // Disable vertical walking controls
        }
        
        public void SetWaterWalkingControls()
        {
            ChangeGrabbing(true);

            CanCrouch = false;
            CanJump = false;
            Teleporter.enabled = false;
            CharacterController.stepOffset = _characterControllerSwimDiveStepHeight;
        }

        public void SetSwimmingControls()
        {
            // Disable grabbing of objects (as in G1)
            ChangeGrabbing(false);

            CanCrouch = false;
            CanJump = false;
            Teleporter.enabled = false;
            CharacterController.stepOffset = _characterControllerSwimDiveStepHeight;

            // Disable vertical walking controls
        }
        
        public void SetDivingControls()
        {
            // Disable grabbing of objects (as in G1)
            ChangeGrabbing(false);

            CanCrouch = false;
            CanJump = false;
            Teleporter.enabled = false;
            CharacterController.stepOffset = _characterControllerSwimDiveStepHeight;

            // Enable vertical walking controls
        }

        private void ChangeGrabbing(bool enable)
        {
            LeftHand.AllowGrabbing = enable;
            LeftHand.ForceGrabber.AllowGrabbing = enable;
            LeftHand.AllowHovering = enable;
            LeftHand.ForceGrabber.AllowHovering = enable;

            RightHand.AllowGrabbing = enable;
            RightHand.ForceGrabber.AllowGrabbing = enable;
            RightHand.AllowHovering = enable;
            RightHand.ForceGrabber.AllowHovering = enable;
            
            // Disable hand animations. Basically open the hand fully if disabled=true
            if (LeftHand.HandAnimator && LeftHand.HandAnimator.CurrentPoser)
            {
                if (LeftHand.HandAnimator.CurrentPoser.PrimaryPose != null)
                {
                    LeftHand.HandAnimator.CurrentPoser.PrimaryPose.Disabled = !enable;
                }

                if (LeftHand.HandAnimator.CurrentPoser.Blends != null)
                {
                    LeftHand.HandAnimator.CurrentPoser.Blends.ForEach(i => i.Disabled = !enable);
                }
            }

            if (RightHand.HandAnimator && RightHand.HandAnimator.CurrentPoser)
            {
                if (RightHand.HandAnimator.CurrentPoser.PrimaryPose != null)
                {
                    RightHand.HandAnimator.CurrentPoser.PrimaryPose.Disabled = !enable;
                }

                if (RightHand.HandAnimator.CurrentPoser.Blends != null)
                {
                    RightHand.HandAnimator.CurrentPoser.Blends.ForEach(i => i.Disabled = !enable);
                }
            }
        }

        private void OnPlayerPrefsUpdated(string preferenceKey, object value)
        {
            // Just update everything.
            if (preferenceKey == VRConstants.IniNames.MoveDirection ||
                preferenceKey == VRConstants.IniNames.RotationType ||
                preferenceKey == VRConstants.IniNames.SnapRotationAmount ||
                preferenceKey == VRConstants.IniNames.SmoothRotationSpeed ||
                preferenceKey == VRConstants.IniNames.SitStand)
            {
                SetNormalControls();
            }
        }

        /// <summary>
        /// Game scenes are all the ones where we play. Aka !=MainMenu, !=Loadings, !=Logo videos, ...
        /// </summary>
        private bool IsGameScene()
        {
            var activeSceneName = SceneManager.GetActiveScene().name;

            return activeSceneName switch
            {
                Constants.SceneBootstrap => false,
                Constants.ScenePlayer => false,
                Constants.SceneGameVersion => false,
                Constants.ScenePreCaching => false,
                Constants.SceneLogo => false,
                Constants.SceneMainMenu => false,
                Constants.SceneLoading => false,
                _ => true
            };
        }

        // protected override void HandleHorizontalMovement()
        // {
        //     if (_playerAi.WalkMode == (int)VmGothicEnums.WalkMode.Swim)
        //     {
        //         
        //     }
        //     else if (_playerAi.WalkMode == (int)VmGothicEnums.WalkMode.Dive)
        //     {
        //         
        //     }
        //     else
        //     {
        //         base.HandleHorizontalMovement();
        //     }
        // }
        //
        // protected override void HandleVerticalMovement()
        // {
        //     if (_playerAi.WalkMode == (int)VmGothicEnums.WalkMode.Swim)
        //     {
        //         
        //     }
        //     else if (_playerAi.WalkMode == (int)VmGothicEnums.WalkMode.Dive)
        //     {
        //         
        //     }
        //     else
        //     {
        //         base.HandleVerticalMovement();
        //     }
        // }
    }
}
#endif
