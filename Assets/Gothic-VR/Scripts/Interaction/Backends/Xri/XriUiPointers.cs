using Gothic.Core.Logging;
using Gothic.VR.Interaction.Models;
using Gothic.VR.Interaction.Ports;
using UnityEngine;
using UnityEngine.EventSystems;
using Logger = Gothic.Core.Logging.Logger;

namespace Gothic.VR.Interaction.Backends.Xri
{
    /// <summary>
    /// NOT IMPLEMENTED YET (HVR phase 1c): XRUIInputModule, TrackedDeviceGraphicRaycaster per canvas and the
    /// interactor-to-hand lookup. Until then, no UI laser pointers exist on the XRI backend and every call only logs
    /// a single warning.
    /// </summary>
    public class XriUiPointers : IVrUiPointers
    {
        private bool _isWarningLogged;

        public void RegisterCanvas(Canvas canvas)
        {
            LogNotImplemented();
        }

        public void UnregisterCanvas(Canvas canvas)
        {
            LogNotImplemented();
        }

        public void RegisterAllCanvases()
        {
            LogNotImplemented();
        }

        public bool TryGetHandSide(PointerEventData eventData, out VrHandSide side)
        {
            LogNotImplemented();
            side = default;
            return false;
        }

        private void LogNotImplemented()
        {
            if (_isWarningLogged)
                return;

            Logger.LogWarning("XRI backend: UI laser pointers aren't implemented yet (HVR phase 1c). " +
                              "Menus can't be clicked with the controllers.", LogCat.VR);
            _isWarningLogged = true;
        }
    }
}
