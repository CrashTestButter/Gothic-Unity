using System;
using UnityEngine;

namespace Gothic.VR.Interaction.Backends.Xri
{
    /// <summary>
    /// Tuning of the player-initiated teleport (right thumbstick forward, release to teleport).
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

        [Tooltip("Radius in m of the sphere cast from the head to the head's position at the target. " +
                 "Blocks teleports through gaps the body doesn't fit through and over walls higher than the head.")]
        public float BodyPathRadius = 0.15f;

        [Tooltip("Maximum height in m the target can be above the feet. Keeps roofs and walls out of reach.")]
        public float MaxRise = 1f;

        [Tooltip("Maximum height in m the target can be below the feet.")]
        public float MaxDrop = 3f;

        [Tooltip("Radius of the target ring in m.")]
        public float ReticleRadius = 0.25f;

        public Color ValidColor = new(0.3f, 0.9f, 0.4f, 0.9f);
        public Color InvalidColor = new(0.9f, 0.3f, 0.3f, 0.6f);
    }
}
