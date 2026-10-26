using System.Collections.Generic;
using UnityEngine;

namespace Gothic.VR.Interaction.Components
{
    /// <summary>
    /// Collisions between own colliders (a physical hand, a held item) and other colliders, ignored for a while after
    /// the own colliders were put somewhere by force (teleport, snap turn, snap back of a stuck hand). Instead of being
    /// pushed out to whatever side is closest (possibly behind a wall), they pass through until they are clear.
    ///
    /// A pair comes back once the own collider no longer overlaps the other one and, if a viewpoint (the head) is
    /// given, the other collider no longer lies between the viewpoint and the own collider. A hand that ended up behind
    /// a wall can therefore be pulled back through it.
    /// </summary>
    internal class VrCollisionPassThrough
    {
        private const int MaxOverlaps = 32;

        private static readonly Collider[] Overlaps = new Collider[MaxOverlaps];
        private static readonly int[] CollidingLayers = new int[32];
        private static bool _isCollidingLayersKnown;

        private readonly List<(Collider Own, Collider Other)> _pairs = new();

        public int Count => _pairs.Count;

        /// <summary>
        /// Ignores the pair until it's clear. Pairs ignored already (held items, the player) are left alone.
        /// </summary>
        public bool Add(Collider own, Collider other)
        {
            if (own == null || other == null || own == other || other.isTrigger)
                return false;

            if (IsSameBody(own, other) || Physics.GetIgnoreCollision(own, other))
                return false;

            Physics.IgnoreCollision(own, other, true);
            _pairs.Add((own, other));
            return true;
        }

        /// <summary>
        /// Adds every other collider one of the own colliders overlaps at its current pose. Moved transforms need a
        /// Physics.SyncTransforms() first.
        /// </summary>
        /// <returns>Number of new pairs.</returns>
        public int AddOverlapping(IReadOnlyList<Collider> own)
        {
            var added = 0;
            foreach (var ownCollider in own)
            {
                if (ownCollider == null || !ownCollider.enabled || ownCollider.isTrigger ||
                    !ownCollider.gameObject.activeInHierarchy)
                    continue;

                var bounds = ownCollider.bounds;
                var count = Physics.OverlapBoxNonAlloc(bounds.center, bounds.extents, Overlaps, Quaternion.identity,
                    GetCollidingLayers(ownCollider.gameObject.layer), QueryTriggerInteraction.Ignore);
                for (var i = 0; i < count; i++)
                {
                    var other = Overlaps[i];
                    if (IsPenetrating(ownCollider, other) && Add(ownCollider, other))
                        added++;
                }
            }

            return added;
        }

        /// <summary>
        /// Restores the pairs that are clear.
        /// </summary>
        /// <param name="viewpoint">The head. Pairs stay ignored while the other collider is between it and the own
        /// collider. null: overlap only.</param>
        public void Update(Vector3? viewpoint)
        {
            for (var i = _pairs.Count - 1; i >= 0; i--)
            {
                var (own, other) = _pairs[i];
                if (own != null && other != null)
                {
                    if (IsPenetrating(own, other) || (viewpoint.HasValue && IsBetween(viewpoint.Value, own, other)))
                        continue;

                    Restore(own, other);
                }

                _pairs.RemoveAt(i);
            }
        }

        /// <summary>
        /// Restores all pairs. Call it before own colliders are deactivated: Unity can't change pairs of inactive
        /// colliders.
        /// </summary>
        public void RestoreAll()
        {
            foreach (var (own, other) in _pairs)
            {
                if (own != null && other != null)
                    Restore(own, other);
            }

            _pairs.Clear();
        }

        private static void Restore(Collider own, Collider other)
        {
            if (own.gameObject.activeInHierarchy && other.gameObject.activeInHierarchy)
                Physics.IgnoreCollision(own, other, false);
        }

        private static bool IsSameBody(Collider own, Collider other)
        {
            var body = own.attachedRigidbody;
            return body != null && body == other.attachedRigidbody;
        }

        private static bool IsPenetrating(Collider own, Collider other)
        {
            if (!own.enabled || !other.enabled)
                return false;

            var ownTransform = own.transform;
            var otherTransform = other.transform;
            return Physics.ComputePenetration(own, ownTransform.position, ownTransform.rotation,
                other, otherTransform.position, otherTransform.rotation, out _, out _);
        }

        private static bool IsBetween(Vector3 viewpoint, Collider own, Collider other)
        {
            var direction = own.bounds.center - viewpoint;
            var distance = direction.magnitude;
            if (distance < 0.001f)
                return false;

            return other.Raycast(new Ray(viewpoint, direction / distance), out _, distance);
        }

        /// <summary>
        /// Layers a collider on the given layer collides with (layer collision matrix).
        /// </summary>
        public static int GetCollidingLayers(int layer)
        {
            if (!_isCollidingLayersKnown)
            {
                for (var i = 0; i < 32; i++)
                {
                    var mask = 0;
                    for (var j = 0; j < 32; j++)
                    {
                        if (!Physics.GetIgnoreLayerCollision(i, j))
                            mask |= 1 << j;
                    }

                    CollidingLayers[i] = mask;
                }

                _isCollidingLayersKnown = true;
            }

            return CollidingLayers[layer];
        }
    }
}
