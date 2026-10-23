using Gothic.Core.Logging;
using Gothic.VR.Interaction.Ports;
using Reflex.Core;
using Logger = Gothic.Core.Logging.Logger;

namespace Gothic.VR.Interaction.Backends.Xri
{
    /// <summary>
    /// XR Interaction Toolkit backend. Always compiled, as XRI is a free package.
    /// Implemented: input, haptics, SFX, UI laser pointers and the player rig (XriRig on "VR - Player XRI.prefab",
    /// with ScreenFade/WallFade on its camera).
    /// Missing: the simulator (HVR phase 1d) and grabbing (phase 2).
    /// </summary>
    public class XriBackend : IVrBackend
    {
        public string Name => "XR Interaction Toolkit";

        public void InstallBindings(ContainerBuilder containerBuilder)
        {
            Logger.LogWarning("XRI backend is work in progress: rig, locomotion, input, haptics, UI laser pointers " +
                              "and screen fades work. The simulator (HVR phase 1d) and grabbing (phase 2) are " +
                              "missing.", LogCat.VR);

            containerBuilder.AddSingleton(typeof(XriInput), typeof(IVrInput), typeof(XriInput));
            containerBuilder.AddSingleton(typeof(XriHaptics), typeof(IVrHaptics));
            containerBuilder.AddSingleton(typeof(XriUiPointers), typeof(IVrUiPointers));
            containerBuilder.AddSingleton(typeof(XriSfxPlayer), typeof(IVrSfxPlayer));
        }

        public bool OwnsRig(IVrRig rig)
        {
            return rig is XriRig;
        }
    }
}
