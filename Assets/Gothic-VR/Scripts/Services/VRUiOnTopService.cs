#if GOTHIC_HVR_INSTALLED
using Gothic.Core;
using Gothic.VR.Adapters.Player;

namespace Gothic.VR.Services
{
    /// <summary>
    /// VR hands above on-top UI (VRUiOnTop), so we see them when pointing at a dialog option. Only while a dialog is
    /// open - otherwise they covered letters/documents held in front of them.
    /// </summary>
    public class VRUiOnTopService
    {
        private VRHandsOnTop _hands;
        private bool _areHandsOnTop;

        public VRUiOnTopService()
        {
            // A scene change in the middle of a dialog never calls EndDialog - put the hands back.
            GlobalEventDispatcher.LoadingSceneLoaded.AddListener(() => SetHandsOnTop(false));
        }

        public void RegisterHands(VRHandsOnTop hands)
        {
            _hands = hands;
            _hands.SetOnTop(_areHandsOnTop);
        }

        public void SetHandsOnTop(bool onTop)
        {
            _areHandsOnTop = onTop;
            if (_hands != null)
                _hands.SetOnTop(onTop);
        }
    }
}
#endif
