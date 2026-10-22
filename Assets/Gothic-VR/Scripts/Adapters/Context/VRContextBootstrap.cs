using System;
using Gothic.Core.Adapters.Context;
using Gothic.Core.Extensions;
using Gothic.Core.Logging;
using Gothic.Core.Models.Context;
using Gothic.Core.Services.Context;
using Gothic.VR.Interaction.Ports;
using Gothic.VR.Services.Context;
using Reflex.Attributes;
using ZenKit;
using Logger = Gothic.Core.Logging.Logger;
#if GOTHIC_HVR_INSTALLED
using Gothic.VR.Interaction.Backends.Hvr;
#endif

namespace Gothic.VR.Adapters.Context
{
    /// <summary>
    /// Bootstrap class which will register listener to set this module as Active if GameSettings.Controls match.
    /// Also selects the VR backend (interaction framework) which provides our VR ports.
    /// </summary>
    public class VRContextBootstrap : AbstractContextBootstrap
    {
        [Inject] private readonly ContextInteractionService _contextInteractionService;
        [Inject] private readonly ContextMenuService _contextMenuService;
        [Inject] private readonly ContextDialogService _contextDialogService;
        [Inject] private readonly ContextTextInputService _contextTextInputService;

        /// <summary>
        /// Returns the VR backend compiled into this build, or null if none is available.
        /// </summary>
        public static IVrBackend SelectBackend()
        {
#if GOTHIC_HVR_INSTALLED
            return new HvrBackend();
#else
            return null;
#endif
        }

        protected override void RegisterControlModule(Controls controls)
        {
            if (controls != Controls.VR)
                return;

            var backend = SelectBackend();
            if (backend == null)
            {
                const string message = "VR controls are set, but no VR backend is available. " +
                                       "Set DeveloperConfig > Controls.GameControls = Flat, or install a VR backend " +
                                       "(e.g. HVR, plus scripting define >GOTHIC_HVR_INSTALLED<).";
                Logger.LogError(message, LogCat.VR);
                throw new ArgumentException(message);
            }

            Logger.Log($"VR backend: {backend.Name}", LogCat.VR);

            // We need to set our VR service now, as the Player.scene loading time (basically this Awake() call) is
            // the first time, when we can set our VR service. But the [Inject] resolves are done already at frame 0
            // at Bootstrap.scene. Therefore, we need to set the VR service via new() and as proxy implementation.
            _contextInteractionService.SetImpl(new VRContextInteractionService());
            _contextMenuService.SetImpl(new VRContextMenuService().Inject());
            _contextDialogService.SetImpl(new VRContextDialogService().Inject());
            _contextTextInputService.SetImpl(new VRContextTextInputService());
        }

        protected override void RegisterGameVersionModule(GameVersion version)
        {
            // NOP
        }
    }
}
