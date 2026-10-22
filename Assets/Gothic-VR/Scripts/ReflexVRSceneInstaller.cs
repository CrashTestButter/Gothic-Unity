#if GOTHIC_HVR_INSTALLED
using Gothic.Core;
using Gothic.VR.Interaction.Backends.Hvr;
using Gothic.VR.Interaction.Ports;
using Gothic.VR.Services;
using Reflex.Core;
using UnityEngine;

namespace Gothic.VR
{
    /// <summary>
    /// Will be automatically called by Reflex when scene is loaded and SceneScope.component is added to the scene.
    /// </summary>
    public class ReflexVRSceneInstaller : MonoBehaviour, IInstaller
    {
        public void InstallBindings(ContainerBuilder containerBuilder)
        {
            containerBuilder.OnContainerBuilt += (container) => ReflexProjectInstaller.DIContainer = container;

            // VR backend (IVrRig lives on the player prefab, see VRContextInteractionService.Rig)
            containerBuilder.AddSingleton(typeof(HvrInput), typeof(IVrInput));
            containerBuilder.AddSingleton(typeof(HvrHaptics), typeof(IVrHaptics));
            containerBuilder.AddSingleton(typeof(HvrUiPointers), typeof(IVrUiPointers));
            containerBuilder.AddSingleton(typeof(HvrSfxPlayer), typeof(IVrSfxPlayer));

            containerBuilder.AddSingleton(typeof(VRPlayerService));
            containerBuilder.AddSingleton(typeof(VRWeaponService));
            containerBuilder.AddSingleton(typeof(VrHapticsService));
            containerBuilder.AddSingleton(typeof(VRCinemaService));
            containerBuilder.AddSingleton(typeof(VRUiOnTopService));
        }
    }
}
#endif
