using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Gothic.VR.Interaction.Backends.Xri
{
    /// <summary>
    /// Player-initiated teleport: pushing the thumbstick forward shows an arc from the hand, releasing it teleports to
    /// the arc's end if the player fits there. The world is created at runtime, so there are no TeleportationArea
    /// interactables. The arc is checked against the physics world instead. The teleport itself is done by XRI's
    /// TeleportationProvider (via the onTeleport callback).
    /// Releasing at an invalid spot (red) does nothing. The cancel button (grip) cancels until the stick is released.
    /// </summary>
    public class XriTeleportAimer : MonoBehaviour
    {
        private const int _reticleSegments = 24;

        [SerializeField] private XriTeleportSettings _settings = new();

        [Tooltip("Line of the aiming arc. Material, width and shadows are set on the component.")]
        [SerializeField] private LineRenderer _arc;

        [Tooltip("Ring at the arc's end. Its points are written at runtime, so it needs Loop enabled.")]
        [SerializeField] private LineRenderer _reticle;

        private Vector3[] _points;

        private InputAction _stickAction;
        private InputAction _cancelAction;
        private Transform _hand;
        private Transform _head;
        private CharacterController _character;
        private LayerMask _blockingLayers;
        private Func<bool> _isAllowed;
        private Func<bool> _isGrounded;
        private Func<float> _getStandingHeight;
        private Action<Vector3, Quaternion> _onTeleport;
        private bool _isInitialized;
        private bool _isAiming;
        private bool _isCanceled;
        private bool _hasTarget;
        private Vector3 _target;

        /// <summary>
        /// Called by XriRig, which owns the rig references. Aiming starts only after this.
        /// </summary>
        public void Init(InputAction stickAction, InputAction cancelAction, Transform hand, Transform head,
            CharacterController character, LayerMask blockingLayers, Func<bool> isAllowed, Func<bool> isGrounded,
            Func<float> getStandingHeight, Action<Vector3, Quaternion> onTeleport)
        {
            _stickAction = stickAction;
            _cancelAction = cancelAction;
            _hand = hand;
            _head = head;
            _character = character;
            _blockingLayers = blockingLayers;
            _isAllowed = isAllowed;
            _isGrounded = isGrounded;
            _getStandingHeight = getStandingHeight;
            _onTeleport = onTeleport;

            _stickAction.Enable();
            _cancelAction?.Enable();

            _points = new Vector3[_settings.ArcSamples + 1];
            _reticle.positionCount = _reticleSegments;
            SetVisible(false);
            _isInitialized = true;
        }

        private void Update()
        {
            if (!_isInitialized)
                return;

            // The Sector interaction (North, Locked) performs when the stick is pushed forward first, stays performed
            // when it's turned sideways and cancels once the stick returns to the center. A stick pushed sideways
            // first (snap turn) never performs.
            if (_stickAction.phase != InputActionPhase.Performed)
            {
                if (_isAiming && _hasTarget && _isAllowed() && _isGrounded())
                    _onTeleport(_target, Quaternion.Euler(0f, _head.eulerAngles.y, 0f));
                Stop();
                _isCanceled = false;
                return;
            }

            if (_isCanceled)
                return;

            if (_isAiming && _cancelAction != null && _cancelAction.WasPressedThisFrame())
            {
                Stop();
                _isCanceled = true;
                return;
            }

            if (!_isAllowed())
            {
                Stop();
                return;
            }

            _isAiming = true;
            UpdateArc();
        }

        private void Stop()
        {
            _isAiming = false;
            _hasTarget = false;
            SetVisible(false);
        }

        private void UpdateArc()
        {
            _hasTarget = false;

            var origin = _hand.position;
            var velocity = _hand.forward * _settings.ArcSpeed;
            var gravity = Vector3.down * _settings.ArcGravity;
            var step = _settings.ArcStep / _settings.ArcSpeed;
            var previous = origin;
            var normal = Vector3.up;
            var isHit = false;
            var count = 0;
            _points[count++] = origin;

            for (var i = 1; i <= _settings.ArcSamples; i++)
            {
                var t = i * step;
                var point = origin + velocity * t + 0.5f * t * t * gravity;
                var segment = point - previous;

                if (Physics.SphereCast(previous, _settings.ArcRadius, segment.normalized, out var hit,
                        segment.magnitude, _blockingLayers, QueryTriggerInteraction.Ignore))
                {
                    _points[count++] = hit.point;
                    _target = hit.point;
                    normal = hit.normal;
                    isHit = true;
                    break;
                }

                _points[count++] = point;
                previous = point;
            }

            // Mid-air (jumping, falling) the arc stays red, so that teleports can't extend jumps or cancel falls.
            _hasTarget = isHit && _isGrounded() && IsValidTarget(_target, normal);

            var color = _hasTarget ? _settings.ValidColor : _settings.InvalidColor;
            _arc.startColor = _arc.endColor = color;
            _arc.positionCount = count;
            _arc.SetPositions(_points);
            _arc.enabled = true;

            if (isHit)
                DrawReticle(_target, normal, color);
            else
                _reticle.enabled = false;
        }

        /// <summary>
        /// Flat enough to stand on, within the height the player could reach, reachable for the body and enough room
        /// for the player's standing capsule (also while crouching, as the player stands up afterwards).
        /// </summary>
        private bool IsValidTarget(Vector3 point, Vector3 normal)
        {
            if (Vector3.Angle(normal, Vector3.up) > _character.slopeLimit)
                return false;

            var feet = _character.bounds.min.y;
            var heightDifference = point.y - feet;
            if (heightDifference > _settings.MaxRise || -heightDifference > _settings.MaxDrop)
                return false;

            var radius = _character.radius;
            var height = Mathf.Max(_character.height, _getStandingHeight());
            var bottom = point + Vector3.up * (radius + _character.stepOffset);
            var top = point + Vector3.up * Mathf.Max(height - radius, radius + _character.stepOffset);

            if (Physics.CheckCapsule(bottom, top, radius * 0.95f, _blockingLayers, QueryTriggerInteraction.Ignore))
                return false;

            // The arc can pass gaps and walls the body can't. The head has to get to where it'll be at the target.
            var head = _head.position;
            var targetHead = point + Vector3.up * (head.y - feet);
            var path = targetHead - head;

            return !Physics.SphereCast(head, _settings.BodyPathRadius, path.normalized, out _, path.magnitude,
                _blockingLayers, QueryTriggerInteraction.Ignore);
        }

        private void DrawReticle(Vector3 center, Vector3 normal, Color color)
        {
            var rotation = Quaternion.FromToRotation(Vector3.up, normal);
            for (var i = 0; i < _reticleSegments; i++)
            {
                var angle = i * Mathf.PI * 2f / _reticleSegments;
                var local = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * _settings.ReticleRadius;
                _reticle.SetPosition(i, center + normal * 0.02f + rotation * local);
            }

            _reticle.startColor = _reticle.endColor = color;
            _reticle.enabled = true;
        }

        private void SetVisible(bool isVisible)
        {
            _arc.enabled = isVisible;
            _reticle.enabled = isVisible;
        }
    }
}
