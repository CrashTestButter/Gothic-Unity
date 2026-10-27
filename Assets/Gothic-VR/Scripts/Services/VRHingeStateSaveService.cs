using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Gothic.Core;
using Gothic.Core.Services.World;
using Gothic.VR.Interaction.Ports;
using Newtonsoft.Json.Linq;
using Reflex.Attributes;
using ZenKit.Vobs;

namespace Gothic.VR.Services
{
    /// <summary>
    /// Doors and chest lids which were left open stay open after loading and when coming back to a world. Gothic's
    /// format has no current mob state (oCMobInter stores stateNum only, the number of its states). Section
    /// "vr.openHinges" stores them per world, by type and position (doors and chests don't move).
    /// </summary>
    public class VRHingeStateSaveService : ISaveStateSection
    {
        public string Key => "vr.openHinges";
        public int Version => 1;

        [Inject] private readonly SaveGameService _saveGameService;

        // World name (upper case, without extension) -> open doors/chests.
        private readonly Dictionary<string, HashSet<string>> _openHinges = new();
        private bool _isInitialized;


        /// <summary>
        /// Called once the VR scope is built (ReflexVRSceneInstaller), i.e. before a save game can be loaded.
        /// </summary>
        public void Init()
        {
            if (_isInitialized)
                return;

            _isInitialized = true;
            _saveGameService.RegisterSection(this);

            // A new game starts with closed doors, a loaded one brings its own (Restore()).
            GlobalEventDispatcher.LoadGameStart.AddListener(_openHinges.Clear);
        }

        /// <summary>
        /// Called once a door/chest of the current world is created: opens it if it was left open, and follows its
        /// hinge from now on.
        /// </summary>
        public void Track(IVrHinge hinge, IVirtualObject mob)
        {
            if (hinge == null || mob == null || string.IsNullOrEmpty(_saveGameService.CurrentWorldName))
                return;

            var openHinges = GetOpenHinges(_saveGameService.CurrentWorldName);
            var key = GetKey(mob);
            if (openHinges.Contains(key))
                hinge.OpenInstantly();

            hinge.Opened += () => openHinges.Add(key);
            hinge.Closed += () => openHinges.Remove(key);
        }

        public JToken Capture()
        {
            var worlds = new JObject();
            foreach (var world in _openHinges.Where(i => i.Value.Count > 0))
                worlds[world.Key] = new JArray(world.Value);

            return worlds;
        }

        public void Restore(JToken data, int savedVersion)
        {
            _openHinges.Clear();
            if (data is not JObject worlds)
                return;

            foreach (var property in worlds.Properties())
            {
                if (property.Value is JArray keys)
                    GetOpenHinges(property.Name).UnionWith(keys.Values<string>());
            }
        }

        private HashSet<string> GetOpenHinges(string worldName)
        {
            // World names differ in casing and extension (World.zen from the ini, WORLD from a save).
            var world = Path.GetFileNameWithoutExtension(worldName).ToUpperInvariant();
            if (!_openHinges.TryGetValue(world, out var openHinges))
            {
                openHinges = new HashSet<string>();
                _openHinges[world] = openHinges;
            }

            return openHinges;
        }

        private static string GetKey(IVirtualObject mob)
        {
            var position = mob.Position;
            return string.Format(CultureInfo.InvariantCulture, "{0}|{1:R}|{2:R}|{3:R}", mob.Type, position.X,
                position.Y, position.Z);
        }
    }
}
