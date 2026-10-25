using System;
using UnityEngine;

namespace Gothic.VR.Interaction.Backends.Xri
{
    /// <summary>
    /// Tuning of the player-initiated teleport (right thumbstick back, release to teleport).
    /// When aiming starts and ends is defined by the Sector interaction of 'XRI Right Locomotion/Teleport Mode'.
    /// </summary>
    [Serializable]
    public class XriTeleportSettings
    {
        [Tooltip("Launch speed of the aiming arc in m/s. Higher values reach farther.")]
        public float ArcSpeed = 9f;

        [Tooltip("Gravity of the aiming arc in m/s^2.")]
        public float ArcGravity = 9.81f;

        [Tooltip("Distance in m between two arc samples.")]
        public float ArcStep = 0.4f;

        [Tooltip("Number of arc samples. Together with ArcStep it limits the teleport distance.")]
        public int ArcSamples = 40;

        [Tooltip("Radius in m of the sphere cast along the arc, so that it doesn't slip through bars and small gaps.")]
        public float ArcRadius = 0.1f;

        [Tooltip("Maximum height in m the target can be above the bottom of the player's capsule " +
                 "(HVR: MaxJumpDistance).")]
        public float MaxJumpDistance = 1.5f;

        [Tooltip("Maximum height in m the target can be below the bottom of the player's capsule " +
                 "(HVR: MaxDropDistance).")]
        public float MaxDropDistance = 3f;

        [Tooltip("Height in m above the target up to which the head needs a free line of sight " +
                 "(HVR: DestinationHeight).")]
        public float LineOfSightHeight = 1f;

        [Tooltip("Number of lines of sight from the head to the target, spread over LineOfSightHeight. " +
                 "One free line is enough (HVR: DestinationIntervals).")]
        public int LineOfSightSamples = 8;

        [Tooltip("Height in m above the arc's hit from where the ground is found with a downward ray. " +
                 "The ray reaches twice as far down.")]
        public float GroundRayHeight = 0.3f;

        [Tooltip("Radius of the target ring in m.")]
        public float ReticleRadius = 0.25f;

        public Color ValidColor = new(0.3f, 0.9f, 0.4f, 0.9f);
        public Color InvalidColor = new(0.9f, 0.3f, 0.3f, 0.6f);
    }
}
