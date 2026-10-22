using Gothic.Core.Logging;
using Gothic.VR.Interaction.Ports;
using Reflex.Core;
using Logger = Gothic.Core.Logging.Logger;

namespace Gothic.VR.Interaction.Backends.Xri
{
    /// <summary>
    /// XR Interaction Toolkit backend. Always compiled, as XRI is a free package.
    /// Implemented: input, haptics, SFX. Missing: the XRI player rig (IVrRig, HVR phase 1b) and the UI laser pointers
    /// (IVrUiPointers, HVR phase 1c; registered as a placeholder which only warns).
    /// </summary>
    public class XriBackend : IVrBackend
    {
        public string Name => "XR Interaction Toolkit";

        public void InstallBindings(ContainerBuilder containerBuilder)
        {
            Logger.LogWarning("XRI backend is work in progress: input and haptics work. The XRI player rig " +
                              "(HVR phase 1b) and UI laser pointers (phase 1c) are missing.", LogCat.VR);

            containerBuilder.AddSingleton(typeof(XriInput), typeof(IVrInput), typeof(XriInput));
            containerBuilder.AddSingleton(typeof(XriHaptics), typeof(IVrHaptics));
            containerBuilder.AddSingleton(typeof(XriUiPointers), typeof(IVrUiPointers));
            containerBuilder.AddSingleton(typeof(XriSfxPlayer), typeof(IVrSfxPlayer));
        }
    }
}
