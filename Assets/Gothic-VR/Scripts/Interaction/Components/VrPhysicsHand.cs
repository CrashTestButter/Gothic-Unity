using System.Collections.Generic;
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
        [Tooltip("Hand side, for logs. Taken from VrBlockHand on the same GameObject if it has one.")] [SerializeField]
        private VrHandSide _side;

        [Tooltip("Material of the ghost hand (transparent).")] [SerializeField]
        private Material _ghostMaterial;

        [Header("Follow")]
        [Tooltip("Mass in kg. Together with Max Force it sets how strongly the hand pushes other rigidbodies.")]
        [SerializeField]
        private float _mass = 1f;

        [Tooltip("Max. speed in m/s the hand moves towards the controller.")] [SerializeField]
        private float _maxSpeed = 15f;

        [Tooltip(
            "Strength limit: max. force in N the hand applies. Pushes items, doors and chest lids, but not NPCs. " +
            "Also limits the acceleration of the free hand (Max Force / Mass).")]
        [SerializeField]
        private float _maxForce = 200f;

        [Tooltip("Max. angular speed in rad/s the hand turns towards the controller.")] [SerializeField]
        private float _maxAngularSpeed = 50f;

        [Tooltip("Max. angular acceleration in rad/s^2.")] [SerializeField]
        private float _maxAngularAcceleration = 2500f;

        [Tooltip("Max. speed in m/s with which the hand is pushed out of colliders it overlaps (after a snap).")]
        [SerializeField]
        private float _maxDepenetrationSpeed = 2f;

        [Header("Ghost hand")]
        [Tooltip("Distance in m between physical hand and controller above which the ghost hand shows.")]
        [SerializeField]
        private float _ghostShowDistance = 0.05f;

        [Tooltip("Distance in m below which the ghost hand hides again.")] [SerializeField]
        private float _ghostHideDistance = 0.03f;

        [Header("Max distance")]
        [Tooltip("Distance in m between physical hand and controller above which the hand counts as stuck.")]
        [SerializeField]
        private float _maxDistance = 0.5f;

        [Tooltip("Time in s a stuck hand waits before it snaps back to the controller.")] [SerializeField]
        private float _maxDistanceTime = 0.2f;

        [Tooltip("A rig turn larger than this (degrees per physics step) is a snap turn: the hand snaps with the rig.")]
        [SerializeField]
        private float _rigSnapAngle = 10f;

        [Tooltip("A rig move larger than this (m per physics step) is a teleport: the hand snaps with the rig.")]
        [SerializeField]
        private float _rigSnapDistance = 1f;

        [Tooltip("Margin in m around the hand: collisions with a released item come back once it is further away.")]
        [SerializeField]
        private float _restoreCollisionMargin = 0.02f;

        private static readonly Dictionary<Rigidbody, VrPhysicsHand> _handsByBody = new();

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

        /// <summary>
        /// How far the controller wants the hand to move from where the last physics step left it (world space).
        /// Kinematic objects the hand touches (VrHinge) use it as the push of the hand. Zero while not physical.
        /// </summary>
        public Vector3 PushOffset => _isActive && _rigidbody != null
            ? _lastTargetPosition - _rigidbody.position
            : Vector3.zero;

        /// <summary>
        /// The physical hand whose rigidbody this is, e.g. the other body of a collision.
        /// </summary>
        public static bool TryGetByBody(Rigidbody body, out VrPhysicsHand hand)
        {
            hand = null;
            return body != null && _handsByBody.TryGetValue(body, out hand);
        }

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
            if (!ReferenceEquals(_rigidbody, null))
                _handsByBody.Remove(_rigidbody);
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
                    return;
                }
            }
            else
            {
                _stuckTime = 0f;
            }

            SyncColliders();
            RestorePendingCollisions();
            Drive(targetPosition, targetRotation, deltaTime);

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

            // The old body may have been destroyed with its scene.
            if (!ReferenceEquals(_rigidbody, null))
                _handsByBody.Remove(_rigidbody);

            _rigidbody = _body.AddComponent<Rigidbody>();
            _handsByBody[_rigidbody] = this;
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
