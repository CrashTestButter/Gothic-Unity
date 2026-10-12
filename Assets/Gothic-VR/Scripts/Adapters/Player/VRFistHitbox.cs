#if GOTHIC_HVR_INSTALLED
using System;
using Gothic.Core.Adapters.Npc;
using Gothic.Core.Const;
using HurricaneVR.Framework.Shared;
using UnityEngine;

namespace Gothic.VR.Adapters.Player
{
    /// <summary>
    /// DeveloperConfig.EnableVrFists: a trigger at the knuckles of an empty VR hand, active while it makes a fist
    /// (grip + trigger). Reports NPC hitboxes it enters - VRWeaponService decides if it's a punch.
    /// </summary>
    public class VRFistHitbox : MonoBehaviour
    {
        private const float _radius = 0.07f;
        private static readonly Vector3 _knuckleOffset = new(0f, 0f, 0.06f);

        public HVRHandSide HandSide { get; private set; }
        public Action<VRFistHitbox, NpcLoader, Vector3> OnNpcEntered;

        public static VRFistHitbox Create(Transform hand, HVRHandSide side)
        {
            var go = new GameObject($"FistHitbox_{side}")
            {
                layer = Constants.VobItemLayer
            };
            go.transform.SetParent(hand, false);
            go.transform.localPosition = _knuckleOffset;

            var sphere = go.AddComponent<SphereCollider>();
            sphere.isTrigger = true;
            sphere.radius = _radius;

            // An own kinematic body - not part of the physics hand's compound collider.
            var body = go.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            var hitbox = go.AddComponent<VRFistHitbox>();
            hitbox.HandSide = side;
            go.SetActive(false);
            return hitbox;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.gameObject.layer != Constants.VobHitbox)
                return;

            var npcLoader = other.GetComponentInParent<NpcLoader>();
            if (npcLoader != null)
                OnNpcEntered?.Invoke(this, npcLoader, transform.position);
        }
    }
}
#endif
