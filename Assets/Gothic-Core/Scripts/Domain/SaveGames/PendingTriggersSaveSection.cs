using System.Linq;
using Gothic.Core.Services.Vobs;
using Gothic.Core.Services.World;
using Newtonsoft.Json.Linq;
using Reflex.Attributes;

namespace Gothic.Core.Domain.SaveGames
{
    /// <summary>
    /// Delayed trigger messages of the current world (e.g. zCTriggerList target delays, VobService) which didn't fire
    /// yet. OpenGothic stores them as trigger events. They fire again with their remaining delay once the world of the
    /// loaded game exists.
    /// </summary>
    public class PendingTriggersSaveSection : ISaveStateSection
    {
        public string Key => "core.pendingTriggers";
        public int Version => 1;

        [Inject] private readonly VobService _vobService;

        private JArray _pendingTriggers;


        public PendingTriggersSaveSection()
        {
            GlobalEventDispatcher.WorldSceneLoaded.AddListener(DispatchRestoredTriggers);
        }

        public JToken Capture()
        {
            return new JArray(_vobService.GetPendingTriggers().Select(i => new JObject
            {
                ["Target"] = i.Target,
                ["Sender"] = i.Sender,
                ["Delay"] = i.RemainingSeconds
            }));
        }

        public void Restore(JToken data, int savedVersion)
        {
            _pendingTriggers = data as JArray;
        }

        private void DispatchRestoredTriggers()
        {
            if (_pendingTriggers == null)
                return;

            foreach (var entry in _pendingTriggers)
            {
                _vobService.DispatchTriggerDelayed(entry.Value<string>("Target"), entry.Value<string>("Sender") ?? "",
                    entry.Value<float>("Delay"));
            }

            _pendingTriggers = null;
        }
    }
}
