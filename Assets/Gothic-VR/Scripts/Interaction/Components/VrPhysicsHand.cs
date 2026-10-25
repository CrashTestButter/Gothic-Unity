using System;
using System.Collections.Generic;
using Gothic.Core;
using Gothic.Core.Logging;
using Gothic.VR.Interaction.Models;
using Gothic.VR.Interaction.Ports;
using UnityEngine;
using UnityEngine.SceneManagement;
using Logger = Gothic.Core.Logging.Logger;

namespace Gothic.VR.Interaction.Components
{
    /// <summary>
    /// Physical hand presence: a rigidbody with one box collider per block of the block hand is driven towards the
    /// tracked controller every FixedUpdate. It stops at walls, tables, chests and NPCs and pushes doors and loose
    /// items with limited strength. A ghost hand shows the real controller pose while the hand is blocked.
    ///
    /// Sits on the visual root of a hand (next to VrBlockHand, child of the tracked controller). The rigidbody lives
    /// on its own GameObject at the root of the player's scene, so that the moving rig (CharacterController) doesn't
    /// drag it kinematically through walls. The visual stays under the controller and is offset by what the physics
    /// step blocked: no interpolation lag while walking, and the late-latched controller pose still applies.
    ///
    /// Off (comfort setting, while climbing): the visual follows the controller directly, the body is inactive.
    /// </summary>
    public class VrPhysicsHand : MonoBehaviour
    {
        [Tooltip("Hand side, for logs. Taken from VrBlockHand on the same GameObject if it has one.")]
        [SerializeField] private VrHandSide _side;

        [Tooltip("Material of the ghost hand (transparent).")]
        [SerializeField] private Material _ghostMaterial;

        [Header("Follow")]
        [Tooltip("Mass in kg. Together with Max Force it sets how strongly the hand pushes other rigidbodies.")]
        [SerializeField] private float _mass = 1f;

        [Tooltip("Max. speed in m/s the hand moves towards the controller.")]
        [SerializeField] private float _maxSpeed = 15f;

        [Tooltip("Strength limit: max. force in N the hand applies. Pushes items, doors and chest lids, but not NPCs. " +
                 "Also limits the acceleration of the free hand (Max Force / Mass).")]
        [SerializeField] private float _maxForce = 200f;

        [Tooltip("Max. angular speed in rad/s the hand turns towards the controller.")]
        [SerializeField] private float _maxAngularSpeed = 50f;

        [Tooltip("Max. angular acceleration in rad/s^2.")]
        [SerializeField] private float _maxAngularAcceleration = 2500f;

        [Tooltip("Max. speed in m/s with which the hand is pushed out of colliders it overlaps (after a snap).")]
        [SerializeField] private float _maxDepenetrationSpeed = 2f;

        [Header("Ghost hand")]
        [Tooltip("Distance in m between physical hand and controller above which the ghost hand shows.")]
        [SerializeField] private float _ghostShowDistance = 0.05f;

        [Tooltip("Distance in m below which the ghost hand hides again.")]
        [SerializeField] private float _ghostHideDistance = 0.03f;

        [Header("Max distance")]
        [Tooltip("Distance in m between physical hand and controller above which the hand counts as stuck.")]
        [SerializeField] private float _maxDistance = 0.5f;

        [Tooltip("Time in s a stuck hand waits before it snaps back to the controller.")]
        [SerializeField] private float _maxDistanceTime = 0.2f;

        [Tooltip("A rig turn larger than this (degrees per physics step) is a snap turn: the hand snaps with the rig.")]
        [SerializeField] private float _rigSnapAngle = 10f;

        [Tooltip("A rig move larger than this (m per physics step) is a teleport: the hand snaps with the rig.")]
        [SerializeField] private float _rigSnapDistance = 1f;

        [Tooltip("Margin in m around the hand: collisions with a released item come back once it is further away.")]
        [SerializeField] private float _restoreCollisionMargin = 0.02f;

        [Header("Held item")]
        [Tooltip("While the held item touches something solid, the hand moves at most this far (m) ahead of where " +
                 "the item holds it. The item follows the hand, so sword and hand stop at a wall together.")]
        [SerializeField] private float _heldLeadDistance = 0.02f;

        [Tooltip("Same as Held Lead Distance for the rotation (degrees).")]
        [SerializeField] private float _heldLeadAngle = 5f;

        [Tooltip("The item doesn't block the hand until its grip got this close (m) to the hand: it's still flying " +
                 "to the hand (distance grab, after a snap).")]
        [SerializeField] private float _heldMaxGripDistance = 0.15f;

        [Tooltip("Time in s the hand stays held back after the item's last blocking contact. Bridges contacts that " +
                 "break for a physics step while the item is pressed against a wall.")]
        [SerializeField] private float _heldBlockedMemory = 0.1f;

        [Tooltip("Dynamic rigidbodies lighter than this (kg) are pushed by the held item, they don't block the hand.")]
        [SerializeField] private float _heldBlockingMinMass = 5f;

        [Header("Contact haptics")]
        [Tooltip("Min. impact speed (m/s, along the contact normal) that gives a contact haptic.")]
        [SerializeField] private float _contactMinSpeed = 0.3f;

        [Tooltip("Min. time in s between two contact haptics of this hand.")]
        [SerializeField] private float _contactCooldown = 0.15f;

        [Tooltip("A new contact gives a haptic only if the hand (or its held item) touched nothing for this long " +
                 "(s). Sliding along a wall or over the edge between two colliders doesn't buzz.")]
        [SerializeField] private float _contactRearmTime = 0.1f;

        private readonly List<(Transform Block, Transform Collider)> _blocks = new();
        private readonly List<Collider> _handColliders = new();
        private readonly HashSet<Collider> _ignoredColliders = new();
        private readonly List<Collider> _pendingRestoreColliders = new();

        private IVrRig _rig;
        private Transform _target;
        private Vector3 _restPosition;
        private Quaternion _restRotation;
        private GameObject _body;
        private Rigidbody _rigidbody;
        private VrGhostHand _ghost;
        private PhysicsMaterial _physicsMaterial;

        private bool _isPhysical = true;
        private bool _isActive;
        private bool _isSnapRequested;
        private bool _isGhostVisible;
        private float _stuckTime;
        private float _separation;
        private Vector3 _lastTargetPosition;
        private Quaternion _lastTargetRotation = Quaternion.identity;
        private Vector3 _lastRigPosition;
        private Quaternion _lastRigRotation = Quaternion.identity;

        private Transform _attachPoint;
        private Rigidbody _heldBody;
        private VrPhysicsHandContact _heldContact;
        private Vector3 _heldGripLocalPosition;
        private Quaternion _heldGripLocalRotation = Quaternion.identity;
        private float _heldBlockedTime = float.NegativeInfinity;
        private bool _isHeldItemSettled;

        private float _lastContactHapticTime = float.NegativeInfinity;
        private float _lastBodyContactTime = float.NegativeInfinity;
        private float _lastHeldContactTime = float.NegativeInfinity;

        /// <summary>
        /// The hand got stuck behind something while holding an item and snapped back to the controller. The holder
        /// releases the item, it would stay stuck otherwise.
        /// </summary>
        public event Action HeldItemStuck;

        /// <summary>
        /// Physical hands (comfort setting). Off: the hand follows the controller directly.
        /// </summary>
        public bool IsPhysical
        {
            get => _isPhysical;
            set => _isPhysical = value;
        }

        /// <summary>
        /// The rigidbody of the physical hand. null before Awake or while the hand was never physical.
        /// Its pose is the pose of the visual root (this transform) as the physics step left it.
        /// </summary>
        public Rigidbody Body => _rigidbody;

        public VrHandSide Side => _side;

        /// <summary>
        /// Distance in m between the physical hand and the controller after the last physics step.
        /// 0 while the hand isn't physical.
        /// </summary>
        public float Separation => _isActive ? _separation : 0f;

        private void Awake()
        {
            var blockHand = GetComponent<VrBlockHand>();
            if (blockHand != null)
                _side = blockHand.Side;

            _target = transform.parent;
            _restPosition = transform.localPosition;
            _restRotation = transform.localRotation;

            _rig = GetComponentInParent<IVrRig>();
            if (_rig == null)
                Logger.LogWarning($"[VrPhysicsHand] {name} ({_side}) has no IVrRig parent. Teleports, snap turns " +
                                  "and climbing aren't handled.", LogCat.VR);

            CreateBody();

            if (_ghostMaterial != null)
                _ghost = VrGhostHand.Create(transform, _restPosition, _restRotation, _ghostMaterial);
            else
                Logger.LogWarning($"[VrPhysicsHand] {name} ({_side}) has no ghost material. No ghost hand.", LogCat.VR);
        }

        private void OnEnable()
        {
            if (_rig != null)
                _rig.Teleported += OnTeleported;
        }

        private void OnDisable()
        {
            if (_rig != null)
                _rig.Teleported -= OnTeleported;

            SetActive(false);
        }

        private void OnDestroy()
        {
            ClearHeldItem();
            if (_body != null)
                Destroy(_body);
            if (_physicsMaterial != null)
                Destroy(_physicsMaterial);
        }

        /// <summary>
        /// Items held or socketed by the player don't collide with the hands (as with the CharacterController).
        /// On restore (item dropped), collisions come back once the item no longer overlaps the hand. Otherwise,
        /// the depenetration would shoot the item away.
        /// </summary>
        public void IgnoreCollisions(IReadOnlyList<Collider> others, bool ignore)
        {
            foreach (var other in others)
            {
                if (other == null)
                    continue;

                if (ignore)
                {
                    _pendingRestoreColliders.Remove(other);
                    // An inactive body gets all ignored pairs when it's activated.
                    if (_ignoredColliders.Add(other) && _isActive)
                        SetCollisionIgnored(other, true);
                }
                else if (!_isActive)
                {
                    // Pairs of an inactive body are reset already.
                    _ignoredColliders.Remove(other);
                }
                else if (_ignoredColliders.Contains(other) && !_pendingRestoreColliders.Contains(other))
                {
                    _pendingRestoreColliders.Add(other);
                }
            }
        }

        /// <summary>
        /// A transform at the world pose of source that moves with the hand visual: with the controller while the
        /// hand is free, stopped where the physics hand is blocked. Grabbers attach held items to it, so the items
        /// follow the physical hand (setting off, climbing: the controller).
        /// </summary>
        public Transform CreateAttachPoint(Transform source)
        {
            if (_attachPoint == null)
            {
                _attachPoint = new GameObject("Physics Hand Attach").transform;
                _attachPoint.SetParent(transform, false);
            }

            if (_target == null)
            {
                _attachPoint.SetPositionAndRotation(source.position, source.rotation);
                return _attachPoint;
            }

            // Relative to the rest pose: the visual may be offset by a blocked hand right now.
            var sourcePosition = _target.InverseTransformPoint(source.position);
            var sourceRotation = Quaternion.Inverse(_target.rotation) * source.rotation;
            var inverseRestRotation = Quaternion.Inverse(_restRotation);
            var localScale = transform.localScale;
            var localPosition = inverseRestRotation * (sourcePosition - _restPosition);
            _attachPoint.SetLocalPositionAndRotation(
                new Vector3(localPosition.x / localScale.x, localPosition.y / localScale.y,
                    localPosition.z / localScale.z),
                inverseRestRotation * sourceRotation);
            return _attachPoint;
        }

        /// <summary>
        /// The hand holds a dynamic item: its collisions give contact haptics, and while it touches something solid,
        /// the hand can't move on without it (the item follows the hand via CreateAttachPoint()).
        /// </summary>
        /// <param name="body">Rigidbody of the item.</param>
        /// <param name="grip">Where the item is held: a transform on the item that matches the attach point once
        /// the item is in the hand.</param>
        public void SetHeldItem(Rigidbody body, Transform grip)
        {
            ClearHeldItem();
            if (body == null || grip == null)
                return;

            _heldBody = body;
            var itemTransform = body.transform;
            _heldGripLocalPosition = itemTransform.InverseTransformPoint(grip.position);
            _heldGripLocalRotation = Quaternion.Inverse(itemTransform.rotation) * grip.rotation;
            _heldBlockedTime = float.NegativeInfinity;
            _lastHeldContactTime = float.NegativeInfinity;
            _isHeldItemSettled = false;

            _heldContact = body.gameObject.AddComponent<VrPhysicsHandContact>();
            _heldContact.Init(this, true);
        }

        public void ClearHeldItem()
        {
            if (_heldContact != null)
                Destroy(_heldContact);

            _heldContact = null;
            _heldBody = null;
            _heldBlockedTime = float.NegativeInfinity;
        }

        /// <summary>
        /// Collision of the body or the held item (VrPhysicsHandContact).
        /// </summary>
        internal void OnContact(Collision collision, bool isHeldItem, bool isEnter)
        {
            var time = Time.fixedTime;
            var lastContactTime = isHeldItem ? _lastHeldContactTime : _lastBodyContactTime;
            if (isHeldItem)
                _lastHeldContactTime = time;
            else
                _lastBodyContactTime = time;

            if (isHeldItem && IsBlocking(collision.rigidbody))
                _heldBlockedTime = time;

            if (!isEnter || !_isActive || collision.contactCount == 0)
                return;

            // Sliding along a wall: contacts with the next collider start while the old ones still touch.
            if (time - lastContactTime < _contactRearmTime || time - _lastContactHapticTime < _contactCooldown)
                return;

            var speed = Mathf.Abs(Vector3.Dot(collision.relativeVelocity, collision.GetContact(0).normal));
            if (speed < _contactMinSpeed)
                return;

            _lastContactHapticTime = time;
            GlobalEventDispatcher.PhysicalHandContact.Invoke((int)_side, speed);
        }

        private bool IsBlocking(Rigidbody other)
        {
            return other == null || other.isKinematic || other.mass >= _heldBlockingMinMass;
        }

        private void Update()
        {
            SetActive(ShouldBeActive());
        }

        private void FixedUpdate()
        {
            if (!_isActive)
                return;

            if (_body == null)
            {
                Logger.LogWarning($"[VrPhysicsHand] The physical {_side} hand was destroyed (scene unload?). " +
                                  "Creating a new one.", LogCat.VR);
                CreateBody();
                ReapplyIgnoredCollisions();
                _isSnapRequested = true;
            }

            var deltaTime = Time.fixedDeltaTime;
            GetTargetPose(out var targetPosition, out var targetRotation);

            // Result of the last step: how far the physics kept the hand from where it should be.
            _separation = Vector3.Distance(_rigidbody.position, _lastTargetPosition);

            if (_isSnapRequested || HasRigJumped())
            {
                Snap(targetPosition, targetRotation);
                return;
            }

            if (_separation > _maxDistance)
            {
                _stuckTime += deltaTime;
                if (_stuckTime > _maxDistanceTime)
                {
                    Logger.LogEditor($"[VrPhysicsHand] {_side} hand stuck {_separation:0.00} m away from the " +
                                     "controller. Snapped back.", LogCat.VR);
                    Snap(targetPosition, targetRotation);
                    ReleaseStuckItem();
                    return;
                }
            }
            else
            {
                _stuckTime = 0f;
            }

            SyncColliders();
            RestorePendingCollisions();

            // A held item that touches a wall holds the hand back. The visual offset and the separation are still
            // measured against the controller (target), so the ghost hand shows and a stuck hand snaps.
            var drivePosition = targetPosition;
            var driveRotation = targetRotation;
            if (TryGetHeldItemHandPose(out var itemHandPosition, out var itemHandRotation))
            {
                drivePosition = itemHandPosition + Vector3.ClampMagnitude(targetPosition - itemHandPosition,
                    _heldLeadDistance);
                driveRotation = Quaternion.RotateTowards(itemHandRotation, targetRotation, _heldLeadAngle);
            }

            Drive(drivePosition, driveRotation, deltaTime);

            _lastTargetPosition = targetPosition;
            _lastTargetRotation = targetRotation;
        }

        private void LateUpdate()
        {
            if (!_isActive)
                return;

            // The visual = the current controller pose plus what the last physics step blocked. Free hands therefore
            // have no lag, blocked hands stay where the physics left them.
            GetTargetPose(out var targetPosition, out var targetRotation);
            var positionOffset = _rigidbody.position - _lastTargetPosition;
            var rotationOffset = _rigidbody.rotation * Quaternion.Inverse(_lastTargetRotation);
            transform.SetPositionAndRotation(targetPosition + positionOffset, rotationOffset * targetRotation);

            UpdateGhost();
        }

        /// <summary>
        /// Where the hand would be on the held item (its grip), while the item is blocked by something solid.
        /// </summary>
        private bool TryGetHeldItemHandPose(out Vector3 position, out Quaternion rotation)
        {
            position = default;
            rotation = default;

            if (_heldBody == null || _attachPoint == null)
                return false;

            var itemTransform = _heldBody.transform;
            var gripRotation = _heldBody.rotation * _heldGripLocalRotation;
            var gripPosition = _heldBody.position +
                               _heldBody.rotation * Vector3.Scale(itemTransform.lossyScale, _heldGripLocalPosition);

            // Hand = grip * inverse(attach point relative to the hand). The body has the visual's scale.
            rotation = gripRotation * Quaternion.Inverse(_attachPoint.localRotation);
            position = gripPosition - rotation * Vector3.Scale(transform.lossyScale, _attachPoint.localPosition);

            // Still flying to the hand (distance grab, after a snap): not held in the hand yet. Once settled, a big
            // gap means the item got wedged: the hand stays with it until it snaps as stuck (and lets go).
            if (!_isHeldItemSettled)
                _isHeldItemSettled = (position - _rigidbody.position).sqrMagnitude <=
                                     _heldMaxGripDistance * _heldMaxGripDistance;

            return _isHeldItemSettled && Time.fixedTime - _heldBlockedTime <= _heldBlockedMemory;
        }

        private void ReleaseStuckItem()
        {
            if (_heldBody == null)
                return;

            Logger.LogEditor($"[VrPhysicsHand] {_side} hand released {_heldBody.name}, it was stuck with the hand.",
                LogCat.VR);
            HeldItemStuck?.Invoke();
        }

        private bool ShouldBeActive()
        {
            if (!_isPhysical || !isActiveAndEnabled)
                return false;

            // Ladders: the ClimbProvider moves the rig by the hand, blocked hands would fight it. (5d)
            return _rig == null || !_rig.IsClimbing;
        }

        private void SetActive(bool isActive)
        {
            if (_isActive == isActive)
                return;

            _isActive = isActive;

            if (isActive)
            {
                if (_body == null)
                    CreateBody();

                _body.SetActive(true);
                ReapplyIgnoredCollisions();
                GetTargetPose(out var targetPosition, out var targetRotation);
                Snap(targetPosition, targetRotation);
            }
            else
            {
                if (_body != null)
                    _body.SetActive(false);

                // Direct follow (5a state).
                transform.SetLocalPositionAndRotation(_restPosition, _restRotation);
                _separation = 0f;
                _stuckTime = 0f;
                SetGhostVisible(false);
            }
        }

        private void CreateBody()
        {
            _blocks.Clear();
            _handColliders.Clear();

            _body = new GameObject($"Physics Hand ({_side})")
            {
                layer = gameObject.layer
            };
            _body.SetActive(false);

            // The player's scene, not the active (world) scene, which is unloaded on world changes.
            if (gameObject.scene.IsValid() && gameObject.scene.isLoaded)
                SceneManager.MoveGameObjectToScene(_body, gameObject.scene);

            _rigidbody = _body.AddComponent<Rigidbody>();
            _body.AddComponent<VrPhysicsHandContact>().Init(this, false);
            _rigidbody.mass = _mass;
            _rigidbody.useGravity = false;
            _rigidbody.linearDamping = 0f;
            _rigidbody.angularDamping = 0f;
            _rigidbody.interpolation = RigidbodyInterpolation.None;
            _rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            _rigidbody.maxAngularVelocity = _maxAngularSpeed;
            _rigidbody.maxDepenetrationVelocity = _maxDepenetrationSpeed;

            if (_physicsMaterial == null)
            {
                _physicsMaterial = new PhysicsMaterial("Physics Hand")
                {
                    dynamicFriction = 0.4f,
                    staticFriction = 0.5f,
                    bounciness = 0f,
                    frictionCombine = PhysicsMaterialCombine.Average,
                    bounceCombine = PhysicsMaterialCombine.Minimum
                };
            }

            // One box per block. The blocks are unit cubes, so the visual is the collider shape.
            foreach (var blockRenderer in GetComponentsInChildren<MeshRenderer>(true))
            {
                var filter = blockRenderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null)
                    continue;

                var colliderGo = new GameObject($"{blockRenderer.name} Collider")
                {
                    layer = gameObject.layer
                };
                colliderGo.transform.SetParent(_body.transform, false);

                var box = colliderGo.AddComponent<BoxCollider>();
                var bounds = filter.sharedMesh.bounds;
                box.center = bounds.center;
                box.size = bounds.size;
                box.sharedMaterial = _physicsMaterial;

                _blocks.Add((blockRenderer.transform, colliderGo.transform));
                _handColliders.Add(box);
            }

            if (_handColliders.Count == 0)
                Logger.LogWarning($"[VrPhysicsHand] {name} ({_side}) has no blocks (MeshRenderer children). The " +
                                  "physical hand has no colliders.", LogCat.VR);

            SyncColliders();

            // Fixed mass properties: the finger colliders move with the grip, which must not change how the hand
            // reacts to contacts.
            _rigidbody.automaticCenterOfMass = false;
            _rigidbody.centerOfMass = Vector3.zero;
            _rigidbody.automaticInertiaTensor = false;
            _rigidbody.inertiaTensor = Vector3.one * (0.001f * _mass);
            _rigidbody.inertiaTensorRotation = Quaternion.identity;
        }

        /// <summary>
        /// The colliders follow the blocks of the visual (finger and thumb curl, rig scale).
        /// </summary>
        private void SyncColliders()
        {
            var scale = transform.lossyScale;
            if (_body.transform.localScale != scale)
                _body.transform.localScale = scale;

            var inverseRootRotation = Quaternion.Inverse(transform.rotation);
            foreach (var (block, blockCollider) in _blocks)
            {
                if (block == null)
                    continue;

                var localPosition = transform.InverseTransformPoint(block.position);
                var localRotation = inverseRootRotation * block.rotation;
                blockCollider.SetLocalPositionAndRotation(localPosition, localRotation);

                var blockScale = block.lossyScale;
                blockCollider.localScale = new Vector3(blockScale.x / scale.x, blockScale.y / scale.y,
                    blockScale.z / scale.z);
            }
        }

        private void Drive(Vector3 targetPosition, Quaternion targetRotation, float deltaTime)
        {
            // Position: the velocity that reaches the target in this step, reached with limited force.
            var desiredVelocity = Vector3.ClampMagnitude((targetPosition - _rigidbody.position) / deltaTime,
                _maxSpeed);
            var maxVelocityChange = _maxForce / _rigidbody.mass * deltaTime;
            var velocityChange = Vector3.ClampMagnitude(desiredVelocity - _rigidbody.linearVelocity,
                maxVelocityChange);
            _rigidbody.AddForce(velocityChange, ForceMode.VelocityChange);

            // Rotation: angular velocity from the quaternion delta.
            var delta = targetRotation * Quaternion.Inverse(_rigidbody.rotation);
            delta.ToAngleAxis(out var angle, out var axis);
            if (angle > 180f)
                angle -= 360f;

            var desiredAngularVelocity = Vector3.zero;
            if (Mathf.Abs(angle) > 0.01f && !float.IsNaN(axis.x) && !float.IsInfinity(axis.x))
                desiredAngularVelocity = Vector3.ClampMagnitude(axis * (angle * Mathf.Deg2Rad / deltaTime),
                    _maxAngularSpeed);

            var angularVelocityChange = Vector3.ClampMagnitude(desiredAngularVelocity - _rigidbody.angularVelocity,
                _maxAngularAcceleration * deltaTime);
            _rigidbody.AddTorque(angularVelocityChange, ForceMode.VelocityChange);
        }

        private void Snap(Vector3 targetPosition, Quaternion targetRotation)
        {
            _isSnapRequested = false;
            _isHeldItemSettled = false;
            _stuckTime = 0f;
            _separation = 0f;

            SyncColliders();
            _body.transform.SetPositionAndRotation(targetPosition, targetRotation);
            _rigidbody.position = targetPosition;
            _rigidbody.rotation = targetRotation;
            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;

            _lastTargetPosition = targetPosition;
            _lastTargetRotation = targetRotation;
            StoreRigPose();
        }

        /// <summary>
        /// Snap turns and teleports move the controller in one step. The hand jumps with them instead of flying.
        /// </summary>
        private bool HasRigJumped()
        {
            if (_rig == null)
                return false;

            var root = _rig.Root;
            var hasJumped = Quaternion.Angle(_lastRigRotation, root.rotation) > _rigSnapAngle
                            || (root.position - _lastRigPosition).sqrMagnitude > _rigSnapDistance * _rigSnapDistance;
            StoreRigPose();
            return hasJumped;
        }

        private void StoreRigPose()
        {
            if (_rig == null)
                return;

            _lastRigPosition = _rig.Root.position;
            _lastRigRotation = _rig.Root.rotation;
        }

        private void OnTeleported(Vector3 position)
        {
            _isSnapRequested = true;
        }

        private void GetTargetPose(out Vector3 position, out Quaternion rotation)
        {
            position = _target.TransformPoint(_restPosition);
            rotation = _target.rotation * _restRotation;
        }

        private void UpdateGhost()
        {
            if (_isGhostVisible)
                SetGhostVisible(_separation > _ghostHideDistance);
            else
                SetGhostVisible(_separation > _ghostShowDistance);
        }

        private void SetGhostVisible(bool isVisible)
        {
            _isGhostVisible = isVisible;
            if (_ghost != null)
                _ghost.SetVisible(isVisible);
        }

        private void SetCollisionIgnored(Collider other, bool ignore)
        {
            foreach (var handCollider in _handColliders)
            {
                if (handCollider != null)
                    Physics.IgnoreCollision(handCollider, other, ignore);
            }
        }

        /// <summary>
        /// Unity resets ignored pairs when a collider is deactivated, which our body is while the hand isn't physical.
        /// </summary>
        private void ReapplyIgnoredCollisions()
        {
            _ignoredColliders.RemoveWhere(i => i == null);
            foreach (var other in _ignoredColliders)
                SetCollisionIgnored(other, true);
        }

        private void RestorePendingCollisions()
        {
            for (var i = _pendingRestoreColliders.Count - 1; i >= 0; i--)
            {
                var other = _pendingRestoreColliders[i];
                if (other != null && IsOverlappingHand(other))
                    continue;

                _pendingRestoreColliders.RemoveAt(i);
                _ignoredColliders.Remove(other);
                if (other != null)
                    SetCollisionIgnored(other, false);
            }
        }

        private bool IsOverlappingHand(Collider other)
        {
            var otherBounds = other.bounds;
            otherBounds.Expand(_restoreCollisionMargin * 2f);
            foreach (var handCollider in _handColliders)
            {
                if (handCollider != null && handCollider.bounds.Intersects(otherBounds))
                    return true;
            }

            return false;
        }
    }
}
