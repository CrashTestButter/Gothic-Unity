#if GOTHIC_HVR_INSTALLED
using System.Collections.Generic;
using Gothic.Core.Const;
using Gothic.Core.Extensions;
using Gothic.Core.Logging;
using Gothic.Core.Services.Config;
using Gothic.VR.Adapters.Vob;
using Reflex.Attributes;
using UnityEngine;
using Logger = Gothic.Core.Logging.Logger;

namespace Gothic.VR.Adapters.Player
{
    /// <summary>
    /// DeveloperConfig.EnableBackpackItemPassThrough: a backpack (the hero's and an NPC's loot backpack) collides with
    /// the world and its vobs only - items (held, lying around or in a holster) pass through it. Items share the
    /// VobItem layer with the mobs' interaction parts (a torch holder's torch, beds, anvils), so no layer can split
    /// them: every item collider coming near the backpack's solid colliders ignores them, pair by pair. Its sockets'
    /// triggers stay untouched (putting an item in still works). Added at runtime by VRBackpack.
    /// </summary>
    public class VRBackpackItemPassThrough : MonoBehaviour
    {
        // An item this far from the backpack ignores it already - before a fast hand pushes it in.
        private const float _margin = 0.3f;

        [Inject] private readonly ConfigService _configService;

        private readonly List<Collider> _solids = new();
        private readonly Collider[] _overlapResults = new Collider[64];
        private int _itemLayerMask;
        private bool _isLogged;

        private void Awake()
        {
            this.Inject();
        }

        private void Start()
        {
            // The backpack's hull - not its sockets' triggers, not the items stored in it.
            foreach (var collider in GetComponentsInChildren<Collider>(true))
            {
                if (!collider.isTrigger && collider.GetComponentInParent<VRVobItem>() == null)
                    _solids.Add(collider);
            }
            _itemLayerMask = (1 << Constants.VobItemLayer) | (1 << LayerMask.NameToLayer("VobItemNoWorldCollision"));
        }

        private void FixedUpdate()
        {
            if (!_configService.Dev.EnableBackpackItemPassThrough || _solids.Count == 0)
                return;

            if (!TryGetBounds(out var bounds))
                return;

            var count = Physics.OverlapSphereNonAlloc(bounds.center, bounds.extents.magnitude + _margin,
                _overlapResults, _itemLayerMask, QueryTriggerInteraction.Ignore);
            for (var i = 0; i < count; i++)
            {
                var other = _overlapResults[i];
                // Items only - not the mobs' parts on the same layer, not the backpack's own content.
                if (other.GetComponentInParent<VRVobItem>() == null)
                    continue;
                IgnoreSolids(other);
            }
        }

        private bool TryGetBounds(out Bounds bounds)
        {
            bounds = default;
            var hasBounds = false;
            foreach (var solid in _solids)
            {
                if (solid == null || !solid.enabled || !solid.gameObject.activeInHierarchy)
                    continue;
                if (hasBounds)
                {
                    bounds.Encapsulate(solid.bounds);
                    continue;
                }
                bounds = solid.bounds;
                hasBounds = true;
            }
            return hasBounds;
        }

        private void IgnoreSolids(Collider item)
        {
            foreach (var solid in _solids)
            {
                // Physics forgets an ignored pair once one collider is disabled (a grip grab, culling) - asked again.
                if (solid == null || !solid.enabled || !solid.gameObject.activeInHierarchy ||
                    Physics.GetIgnoreCollision(solid, item))
                    continue;
                Physics.IgnoreCollision(solid, item, true);
                if (_isLogged)
                    continue;
                _isLogged = true;
                Logger.Log($"[BackpackPassThrough] {name}: items pass through (first: {item.name})", LogCat.VR);
            }
        }
    }
}
#endif
