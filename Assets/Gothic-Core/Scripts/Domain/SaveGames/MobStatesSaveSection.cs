using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Gothic.Core.Adapters.Properties.Vobs;
using Gothic.Core.Services.Caches;
using Gothic.Core.Services.World;
using Newtonsoft.Json.Linq;
using Reflex.Attributes;
using ZenKit.Vobs;

namespace Gothic.Core.Domain.SaveGames
{
    /// <summary>
    /// Current states of mobs (InteractiveProperties.State): wheels and levers, open doors and chests, ... Gothic's
    /// format has none (oCMobInter stores stateNum only, the number of its states). Stored per world, by type and
    /// position (mobs don't move). Mobs in state 0 aren't stored.
    /// Leaving a world keeps its states for a revisit in the same session.
    /// </summary>
    public class MobStatesSaveSection : ISaveStateSection
    {
        public string Key => "core.mobStates";
        public int Version => 1;

        [Inject] private readonly SaveGameService _saveGameService;
        [Inject] private readonly MultiTypeCacheService _multiTypeCacheService;

        // World name (upper case, without extension) -> mob key -> state.
        private readonly Dictionary<string, Dictionary<string, int>> _states = new();


        public MobStatesSaveSection()
        {
            // A new game starts with all mobs in state 0, a loaded one brings its states (Restore()).
            GlobalEventDispatcher.LoadGameStart.AddListener(_states.Clear);
            GlobalEventDispatcher.StoreWorldStateStart.AddListener(_ => StoreCurrentWorld());
            GlobalEventDispatcher.WorldSceneLoaded.AddListener(RestoreCurrentWorld);
        }

        public JToken Capture()
        {
            StoreCurrentWorld();

            var worlds = new JObject();
            foreach (var world in _states)
            {
                if (world.Value.Count == 0)
                    continue;

                var states = new JObject();
                foreach (var state in world.Value)
                    states[state.Key] = state.Value;
                worlds[world.Key] = states;
            }

            return worlds;
        }

        public void Restore(JToken data, int savedVersion)
        {
            _states.Clear();
            if (data is not JObject worlds)
                return;

            foreach (var world in worlds.Properties())
            {
                if (world.Value is not JObject states)
                    continue;

                var worldStates = new Dictionary<string, int>();
                foreach (var state in states.Properties())
                    worldStates[state.Name] = state.Value.Value<int>();
                _states[world.Name] = worldStates;
            }
        }

        private void StoreCurrentWorld()
        {
            if (string.IsNullOrEmpty(_saveGameService.CurrentWorldName))
                return;

            var states = new Dictionary<string, int>();
            foreach (var vobContainer in _multiTypeCacheService.VobCache)
            {
                if (vobContainer?.Props is InteractiveProperties { State: > 0 } props)
                    states[GetKey(vobContainer.Vob)] = props.State;
            }

            _states[GetWorldKey(_saveGameService.CurrentWorldName)] = states;
        }

        /// <summary>
        /// The world's mobs exist now. Their GameObjects (e.g. a door's hinge) are created later and read the state.
        /// </summary>
        private void RestoreCurrentWorld()
        {
            if (string.IsNullOrEmpty(_saveGameService.CurrentWorldName) ||
                !_states.TryGetValue(GetWorldKey(_saveGameService.CurrentWorldName), out var states))
                return;

            foreach (var vobContainer in _multiTypeCacheService.VobCache)
            {
                if (vobContainer?.Props is InteractiveProperties props &&
                    states.TryGetValue(GetKey(vobContainer.Vob), out var state))
                    props.State = state;
            }
        }

        private static string GetWorldKey(string worldName)
        {
            // World names differ in casing and extension (World.zen from the ini, WORLD from a save).
            return Path.GetFileNameWithoutExtension(worldName).ToUpperInvariant();
        }

        private static string GetKey(IVirtualObject mob)
        {
            var position = mob.Position;
            return string.Format(CultureInfo.InvariantCulture, "{0}|{1:R}|{2:R}|{3:R}", mob.Type, position.X,
                position.Y, position.Z);
        }
    }
}
