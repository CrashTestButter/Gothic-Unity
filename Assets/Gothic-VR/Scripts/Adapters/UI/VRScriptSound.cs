#if GOTHIC_HVR_INSTALLED
using Gothic.Core.Manager;
using UnityEngine;

namespace Gothic.VR.Adapters.UI
{
    /// <summary>
    /// DeveloperConfig.EnableScreenMessages: Daedalus Snd_Play 2D sounds ("LogEntry") next to the PrintScreen texts
    /// (VRPrintScreen).
    /// </summary>
    public class VRScriptSound : MonoBehaviour
    {
        private AudioService _audioService;
        private AudioSource _audioSource;

        /// <summary>
        /// Creates the sound source in the active scene. It's destroyed with the scene, owners create a new one then.
        /// </summary>
        public static VRScriptSound Create(AudioService audioService)
        {
            var go = new GameObject("_ScriptSound");
            var scriptSound = go.AddComponent<VRScriptSound>();
            scriptSound._audioService = audioService;
            scriptSound._audioSource = go.AddComponent<AudioSource>();
            scriptSound._audioSource.spatialBlend = 0f;
            scriptSound._audioSource.playOnAwake = false;
            return scriptSound;
        }

        public void PlaySound(string soundName)
        {
            var clip = _audioService.GetRandomSoundClip(soundName);
            if (clip != null)
                _audioSource.PlayOneShot(clip);
        }
    }
}
#endif
