using System;
using UnityEngine;

namespace Gothic.Core.Adapters.Npc
{
    /// <summary>
    /// DeveloperConfig.EnableNpcSpellFx: the visible flight of an NPC's spell (its CAST key PFX) to the target. The hit
    /// happens when it arrives (OnArrive), like in the engine, not the moment the cast animation reaches its frame.
    /// </summary>
    public class NpcSpellFlight : MonoBehaviour
    {
        private const float _arriveDistance = 0.3f;
        private const float _targetHeight = 1f;

        public Transform Target;
        public float Speed = 15f;
        public float MaxSeconds = 4f;
        public Action OnArrive;

        private float _endTime;
        private bool _hasArrived;

        private void Start()
        {
            _endTime = Time.time + MaxSeconds;
        }

        private void Update()
        {
            if (_hasArrived)
                return;

            // The target is gone (despawned): the spell fades out without a hit.
            if (Target == null)
            {
                Destroy(gameObject);
                return;
            }

            var destination = Target.position + Vector3.up * _targetHeight;
            var toTarget = destination - transform.position;
            var step = Speed * Time.deltaTime;
            if (toTarget.magnitude <= Mathf.Max(step, _arriveDistance) || Time.time >= _endTime)
            {
                Arrive();
                return;
            }

            transform.SetPositionAndRotation(transform.position + toTarget.normalized * step,
                Quaternion.LookRotation(toTarget));
        }

        private void Arrive()
        {
            _hasArrived = true;
            try
            {
                OnArrive?.Invoke();
            }
            finally
            {
                foreach (var particles in GetComponentsInChildren<ParticleSystem>(true))
                    particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                Destroy(gameObject, 1f);
            }
        }
    }
}
