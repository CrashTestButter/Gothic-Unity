#if GOTHIC_HVR_INSTALLED
using Gothic.Core.Services.Config;
using Gothic.Core.Services.Context;
using Gothic.VR.Adapters.HVROverrides;
using Gothic.Core;
using HurricaneVR.Framework.ControllerInput;
using Reflex.Attributes;
using UnityEngine;

namespace Gothic.VR.Adapters.UI
{
    public class VRMenuBillboard : MonoBehaviour
    {
        [Header("Position Settings")]
        // Y offset will be ignored as we use the player height
        [SerializeField]
        private Vector3 _positionOffset = new(0, 0, 5f);

        private Vector3 _finalPositionOffset = Vector3.zero;

        [SerializeField] private float _positionSmoothTime = 0.3f;

        [Inject] private readonly ContextInteractionService _contextInteractionService;
        [Inject] private readonly ConfigService _configService;

        // DeveloperConfig.EnableMenuZoom: the right stick pulls the menu closer (down) or pushes it away (up) - from 5 m
        // the save list was unreadable. Kept for the session, the menu opens again where it was left.
        private const float _minDistance = 1f;
        private const float _maxDistance = 8f;
        private const float _zoomSpeed = 2.5f;
        private const float _stickDeadZone = 0.2f;
        private static float? _zoomDistance;
        private Vector3 _flatForward;

        private Transform _playerTransform;
        private VRPlayerController _playerController;
        private Vector3 _moveVelocity;
        private float _currentYaw;

        private void OnEnable()
        {
            if (_playerTransform == null)
            {
                _playerTransform = _contextInteractionService.GetCurrentPlayerController().transform;
                _playerController = _playerTransform.GetComponent<VRPlayerController>();
            }

            RecalculatePositionOffset();
            UpdateOffset(true);
            transform.rotation = _playerTransform.rotation;
        }

        private void Update()
        {
            if (_playerTransform == null) return;

            UpdateZoom();
            UpdateOffset();
        }

        private void RecalculatePositionOffset()
        {
            // We want the position offset on the x axis to be recalculated only when turning on the menu
            _flatForward = Vector3.ProjectOnPlane(_playerTransform.forward, Vector3.up).normalized;
            var distance = _configService.Dev.EnableMenuZoom ? _zoomDistance ?? _positionOffset.z : _positionOffset.z;
            _finalPositionOffset = _flatForward * distance;
        }

        private void UpdateZoom()
        {
            if (!_configService.Dev.EnableMenuZoom || HVRInputManager.Instance == null)
                return;
            var controller = HVRInputManager.Instance.RightController;
            if (controller == null || Mathf.Abs(controller.JoystickAxis.y) < _stickDeadZone)
                return;

            var distance = _zoomDistance ?? _positionOffset.z;
            _zoomDistance = Mathf.Clamp(distance + controller.JoystickAxis.y * _zoomSpeed * Time.unscaledDeltaTime,
                _minDistance, _maxDistance);
            _finalPositionOffset = _flatForward * _zoomDistance.Value;
        }

        private void UpdateOffset(bool immediate = false)
        {
            // Calculate target position with proper height tracking
            Vector3 targetPosition = _playerTransform.position + _finalPositionOffset +
                                     Vector3.up * _playerController.CameraHeight;

            if (immediate)
            {
                transform.position = targetPosition;
            }
            else
            {
                transform.position = Vector3.SmoothDamp(
                    transform.position,
                    targetPosition,
                    ref _moveVelocity,
                    _positionSmoothTime
                );
            }
        }
    }
}
#endif
