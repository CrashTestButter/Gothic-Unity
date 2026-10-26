using Newtonsoft.Json.Linq;

namespace Gothic.Core.Services.World
{
    /// <summary>
    /// State which Gothic's save format doesn't store (or ZenKit can't write correctly) goes into UNITYSAVE.json.
    /// Every system registers its own section via SaveGameService.RegisterSection(). SaveGameService doesn't need
    /// to know the sections.
    /// </summary>
    public interface ISaveStateSection
    {
        /// <summary>
        /// Unique key, prefixed by its owner: "core.*", "vr.*", "flat.*" or "{modId}.*".
        /// </summary>
        string Key { get; }

        /// <summary>
        /// Increase whenever the data format changes. Restore() gets the version which was saved.
        /// </summary>
        int Version { get; }

        JToken Capture();

        /// <summary>
        /// Called when a save game is loaded, before its first world is created. Sections which need the world
        /// keep the data and apply it later (e.g. on GlobalEventDispatcher.WorldSceneLoaded).
        /// </summary>
        void Restore(JToken data, int savedVersion);
    }
}
