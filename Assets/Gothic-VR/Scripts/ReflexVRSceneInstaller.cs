using Gothic.Core;
using Gothic.Core.Logging;
using Gothic.VR.Adapters.Context;
using Gothic.VR.Services;
using Reflex.Core;
using UnityEngine;
using Logger = Gothic.Core.Logging.Logger;

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

            // VR ports (IVrInput, IVrHaptics, ...) come from the active backend.
            // Without a backend, nothing is registered and VRContextBootstrap fails the boot with a clear error.
            var backend = VRContextBootstrap.SelectBackend();
            if (backend == null)
                Logger.LogError("No VR backend available. VR ports aren't registered.", LogCat.VR);
            else
                backend.InstallBindings(containerBuilder);

            containerBuilder.AddSingleton(typeof(VRPlayerService));
            containerBuilder.AddSingleton(typeof(VRWeaponService));
            containerBuilder.AddSingleton(typeof(VrHapticsService));
            containerBuilder.AddSingleton(typeof(VRCinemaService));
            containerBuilder.AddSingleton(typeof(VRUiOnTopService));
        }
    }
}
