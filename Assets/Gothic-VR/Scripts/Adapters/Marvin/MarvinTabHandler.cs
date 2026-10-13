#if GOTHIC_HVR_INSTALLED
using Gothic.Core.Logging;
using Gothic.Core.Models.Caches;
using Gothic.Core.Models.Vm;
using Gothic.Core.Services.Caches;
using Gothic.Core.Services.Config;
using Gothic.Core.Services.Context;
using Gothic.Core.Services.Npc;
using Gothic.Core.Services.World;
using Gothic.Core.Creator;
using Reflex.Attributes;
using TMPro;
using UnityEngine;
using ZenKit.Daedalus;
using UnityEngine.UI;
using Logger = Gothic.Core.Logging.Logger;
using LogCat = Gothic.Core.Logging.LogCat;

namespace Gothic.VR.Adapters.Marvin
{
    public class MarvinTabHandler : MonoBehaviour
    {
        [Inject] private readonly ConfigService _configService;
        [Inject] private readonly GameTimeService _gameTimeService;
        [Inject] private readonly NpcRoutineService _npcRoutineService;
        [Inject] private readonly NpcService _npcService;
        [Inject] private readonly ResourceCacheService _resourceCacheService;
        [Inject] private readonly WayNetService _wayNetService;
        [Inject] private readonly ContextInteractionService _contextInteractionService;
        [Inject] private readonly Gothic.Core.Services.Player.PlayerService _playerService;
        [Inject] private readonly VmCacheService _vmCacheService;

        private void Start()
        {
            var placeholder = transform.Find("Text");
            if (placeholder != null)
                placeholder.gameObject.SetActive(false);

            CreateButtons();
        }

        private void CreateButtons()
        {
            var buttons = new System.Collections.Generic.List<(string label, System.Action onClick)>();

            if (_configService.Dev.EnableLevel5Cheat)
                buttons.Add(("Level +5", CheatAddLevels));

            if (_configService.Dev.EnableGuildCheat)
                buttons.Add(("Guild → Novice", CheatToNovice));

            if (_configService.Dev.EnableScavengerGuildCheat)
                buttons.Add(("Guild → Scavenger", CheatToScavenger));

            if (_configService.Dev.EnableTimeSkip)
                buttons.Add(("Skip Time +30min", SkipTime30Min));

            if (_configService.Dev.EnableMarvinBlink)
                buttons.Add(("TP +4 m", CheatBlinkForward));

            if (_configService.Dev.EnableMarvinGodMode)
                buttons.Add(("God mode", CheatToggleGodMode));

            if (!string.IsNullOrEmpty(_configService.Dev.MarvinTeleportWaypoint))
                buttons.Add(($"TP → {_configService.Dev.MarvinTeleportWaypoint}", CheatTeleportToWaypoint));

            if (!string.IsNullOrEmpty(_configService.Dev.MarvinSpawnNpcSymbol))
                buttons.Add(($"Spawn {_configService.Dev.MarvinSpawnNpcSymbol}", CheatSpawnNpc));

            if (!string.IsNullOrEmpty(_configService.Dev.MarvinFriendsNpcSymbols))
                buttons.Add(("Irdo team", () => CheatSpawnGroup("Irdo team", _configService.Dev.MarvinFriendsNpcSymbols)));

            if (!string.IsNullOrEmpty(_configService.Dev.MarvinCheatFriendsNpcSymbols))
                buttons.Add(("Cheat friends", () => CheatSpawnGroup("Cheat friends", _configService.Dev.MarvinCheatFriendsNpcSymbols)));

            if (!string.IsNullOrEmpty(_configService.Dev.MarvinGiveItems))
                buttons.Add(("Give items", CheatGiveItems));

            // More options than fit the panel: a window of them with Up/Down buttons above and below.
            if (buttons.Count <= _maxVisibleButtons)
            {
                var startY = GetButtonsHeight(buttons.Count) / 2f - _buttonHeight / 2f;
                for (var i = 0; i < buttons.Count; i++)
                {
                    var (label, onClick) = buttons[i];
                    CreateButton(label, onClick, startY - i * (_buttonHeight + _buttonGap));
                }
                return;
            }

            foreach (var (label, onClick) in buttons)
            {
                var go = CreateButton(label, onClick, 0f);
                if (go != null)
                    _scrollButtons.Add(go);
            }

            var windowTop = GetButtonsHeight(_maxVisibleButtons + 2) / 2f - _buttonHeight / 2f;
            CreateButton("Up", () => ScrollButtons(-1), windowTop);
            CreateButton("Down", () => ScrollButtons(1), windowTop - (_maxVisibleButtons + 1) * (_buttonHeight + _buttonGap));
            _firstVisibleButton = 0;
            LayoutScrollButtons(windowTop - (_buttonHeight + _buttonGap));
        }

        private const float _buttonHeight = 50f;
        private const float _buttonGap = 10f;
        private const int _maxVisibleButtons = 6;
        private readonly System.Collections.Generic.List<GameObject> _scrollButtons = new();
        private int _firstVisibleButton;
        private float _scrollWindowTop;

        private static float GetButtonsHeight(int count)
        {
            return count * _buttonHeight + (count - 1) * _buttonGap;
        }

        /// <summary>
        /// Moves the window of visible options by one.
        /// </summary>
        private void ScrollButtons(int delta)
        {
            var maxFirst = Mathf.Max(0, _scrollButtons.Count - _maxVisibleButtons);
            _firstVisibleButton = Mathf.Clamp(_firstVisibleButton + delta, 0, maxFirst);
            LayoutScrollButtons(_scrollWindowTop);
        }

        private void LayoutScrollButtons(float windowTop)
        {
            _scrollWindowTop = windowTop;
            for (var i = 0; i < _scrollButtons.Count; i++)
            {
                var slot = i - _firstVisibleButton;
                var isVisible = slot >= 0 && slot < _maxVisibleButtons;
                _scrollButtons[i].SetActive(isVisible);
                if (isVisible && _scrollButtons[i].TryGetComponent<RectTransform>(out var rt))
                    rt.anchoredPosition = new Vector2(0f, windowTop - slot * (_buttonHeight + _buttonGap));
            }
        }

        private const int _manaMaxPerLevelCheat = 100;

        private void CheatAddLevels()
        {
            const int levelsToAdd = 5;
            var hero = _npcService.GetHeroContainer();
            var oldLevel = hero.Instance.Level;
            hero.Instance.Level += levelsToAdd;
            hero.Instance.Lp += levelsToAdd * 10;
            // HP and mana max up, both filled. Set on the instance too: SyncHeroInstanceToVob() copies the instance
            // into the vob - vob-only values were reverted at once.
            var hpMax = hero.Instance.GetAttribute(NpcAttribute.HitPointsMax) + levelsToAdd * 12;
            var manaMax = hero.Instance.GetAttribute(NpcAttribute.ManaMax) + _manaMaxPerLevelCheat;
            hero.Instance.SetAttribute(NpcAttribute.HitPointsMax, hpMax);
            hero.Instance.SetAttribute(NpcAttribute.HitPoints, hpMax);
            hero.Instance.SetAttribute(NpcAttribute.ManaMax, manaMax);
            hero.Instance.SetAttribute(NpcAttribute.Mana, manaMax);
            _npcService.SyncHeroInstanceToVob();
            Logger.Log($"[MarvinMode] Level cheat: {oldLevel}→{hero.Instance.Level} (+{levelsToAdd * 10} LP, " +
                       $"HP {hpMax}/{hpMax}, mana {manaMax}/{manaMax})", LogCat.Ui);
        }

        private void CheatToNovice()
        {
            var hero = _npcService.GetHeroContainer();
            hero.Instance.Guild = (int)VmGothicEnums.Guild.GIL_NOV;
            Logger.Log("[MarvinMode] Guild cheat: set to GIL_NOV", LogCat.Ui);
        }

        private void CheatToScavenger()
        {
            var hero = _npcService.GetHeroContainer();
            hero.Instance.Guild = (int)VmGothicEnums.Guild.GIL_SCAVENGER;
            Logger.Log("[MarvinMode] Guild cheat: set to GIL_SCAVENGER (24)", LogCat.Ui);
        }

        private void CheatTeleportToWaypoint()
        {
            var wpName = _configService.Dev.MarvinTeleportWaypoint;
            var wp = _wayNetService.GetWayNetPoint(wpName);
            if (wp == null)
            {
                Logger.LogWarning($"[MarvinMode] Teleport WP '{wpName}' not found", LogCat.Ui);
                return;
            }
            _contextInteractionService.TeleportPlayerTo(wp.Position, wp.Rotation);
            Logger.Log($"[MarvinMode] Teleported to '{wpName}'", LogCat.Ui);
        }

        /// <summary>
        /// DeveloperConfig.EnableMarvinGodMode: hits (melee, arrows, spells) and falls don't hurt the hero. Toggles; the
        /// state shows as a screen message.
        /// </summary>
        private void CheatToggleGodMode()
        {
            _playerService.IsGodMode = !_playerService.IsGodMode;
            var state = _playerService.IsGodMode ? "ON" : "OFF";
            Gothic.Core.GlobalEventDispatcher.ScriptPrintScreen.Invoke($"God mode {state}", 0, 2);
            Logger.Log($"[MarvinMode] God mode {state}", LogCat.Ui);
        }

        private const float _blinkDistance = 4f;
        private const float _blinkFloorSearchHeight = 3f;

        /// <summary>
        /// DeveloperConfig.EnableMarvinBlink - like Gothic's Marvin key K: 4 m forward where the hero looks, only
        /// horizontally (through doors and walls). The floor below the target is searched from a bit above, otherwise
        /// the height stays. Held items stay in the hands (no TeleportPlayerTo). Falling out: MarvinFallRescue.
        /// </summary>
        private void CheatBlinkForward()
        {
            var player = _contextInteractionService.GetCurrentPlayerController();
            var cam = Camera.main;
            if (player == null || cam == null)
                return;

            var forward = cam.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
                forward = player.transform.forward;
            forward.y = 0f;
            forward.Normalize();

            var target = player.transform.position + forward * _blinkDistance;
            if (Physics.Raycast(target + Vector3.up * _blinkFloorSearchHeight, Vector3.down, out var hit,
                    _blinkFloorSearchHeight * 2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                target.y = hit.point.y;

            var controller = player.GetComponent<Gothic.VR.Adapters.HVROverrides.VRPlayerController>();
            if (controller?.Teleporter == null)
                return;
            controller.Teleporter.Teleport(target);
            Logger.Log($"[MarvinMode] Blink {_blinkDistance:F0} m forward to {target}", LogCat.Ui);
        }

        private void CheatSpawnNpc()
        {
            var symbol = _configService.Dev.MarvinSpawnNpcSymbol;
            var hero = _npcService.GetHeroContainer();
            if (hero?.Go == null)
            {
                Logger.LogWarning($"[MarvinMode] Spawn NPC: hero GO not found", LogCat.Ui);
                return;
            }
            var pos = hero.Go.transform.position + hero.Go.transform.forward * 2f;
            var rot = hero.Go.transform.rotation;
            var ok = _npcService.SpawnNpcByName(symbol, pos, rot);
            if (ok)
                Logger.Log($"[MarvinMode] Spawned '{symbol}' near player", LogCat.Ui);
        }

        private const float _friendsDistance = 2.5f;
        private const float _friendsArcDegrees = 120f;

        /// <summary>
        /// DeveloperConfig.MarvinFriendsNpcSymbols: all of them at once, on an arc in front of the hero.
        /// </summary>
        /// <summary>
        /// Spawns a group of NPCs in an arc in front of the hero, facing him: "Irdo team" (DeveloperConfig
        /// MarvinFriendsNpcSymbols - the Irdorath crew) or "Cheat friends" (MarvinCheatFriendsNpcSymbols - SH/CH).
        /// </summary>
        private void CheatSpawnGroup(string groupName, string npcSymbols)
        {
            var hero = _npcService.GetHeroContainer();
            if (hero?.Go == null)
            {
                Logger.LogWarning($"[MarvinMode] {groupName}: hero GO not found", LogCat.Ui);
                return;
            }

            var symbols = npcSymbols.Split(',', ';', '\n');
            var names = new System.Collections.Generic.List<string>();
            foreach (var entry in symbols)
            {
                var name = entry.Trim();
                if (name.Length > 0)
                    names.Add(name);
            }

            var forward = hero.Go.transform.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
            for (var i = 0; i < names.Count; i++)
            {
                var angle = names.Count > 1 ? -_friendsArcDegrees / 2f + _friendsArcDegrees * i / (names.Count - 1) : 0f;
                var direction = Quaternion.Euler(0f, angle, 0f) * forward;
                var position = hero.Go.transform.position + direction * _friendsDistance;
                // Facing the hero.
                var rotation = Quaternion.LookRotation(-direction);
                if (_npcService.SpawnNpcByName(names[i], position, rotation))
                    Logger.Log($"[MarvinMode] {groupName}: spawned '{names[i]}'", LogCat.Ui);
            }
        }

        /// <summary>
        /// DeveloperConfig.MarvinGiveItems: "ITRU_FIREBOLT, ITPO_MANA_03:5" - into the hero's inventory (backpack).
        /// </summary>
        private void CheatGiveItems()
        {
            foreach (var entry in _configService.Dev.MarvinGiveItems.Split(',', ';', '\n'))
            {
                var parts = entry.Trim().Split(':');
                var itemName = parts[0].Trim().ToUpper();
                if (itemName.Length == 0)
                    continue;
                var amount = parts.Length > 1 && int.TryParse(parts[1].Trim(), out var parsed) ? Mathf.Max(1, parsed) : 1;

                if (_vmCacheService.TryGetItemData(itemName) == null)
                {
                    Logger.LogWarning($"[MarvinMode] Give items: unknown item '{itemName}'", LogCat.Ui);
                    continue;
                }
                _playerService.AddItem(itemName, amount);
                Logger.Log($"[MarvinMode] Gave {itemName} x{amount}", LogCat.Ui);
            }
        }

        private void SkipTime30Min()
        {
            var t = _gameTimeService.GetCurrentTime();
            var next = t.Add(System.TimeSpan.FromMinutes(30));
            _gameTimeService.SetTime(next.Hours, next.Minutes);
            Gothic.Core.GlobalEventDispatcher.GameTimeSkipped.Invoke();
            _npcRoutineService.RecalculateAllNpcRoutines();
            Logger.Log($"[MarvinMode] Time skip → {next.Hours:D2}:{next.Minutes:D2}", LogCat.Ui);
        }

        private GameObject CreateButton(string label, System.Action onClick, float anchoredY)
        {
            var go = _resourceCacheService.TryGetPrefabObject(PrefabType.UiDebugButton, parent: gameObject);
            if (go == null)
            {
                Logger.LogWarning($"[MarvinMode] UiDebugButton prefab not found for: {label}", LogCat.Ui);
                return null;
            }

            var rt = go.GetComponent<RectTransform>();
            if (rt != null)
                rt.anchoredPosition = new Vector2(0f, anchoredY);

            var text = go.GetComponentInChildren<TMP_Text>();
            if (text != null)
                text.text = label;

            var button = go.GetComponentInChildren<Button>();
            if (button != null)
                button.onClick.AddListener(() => onClick());
            return go;
        }
    }
}
#endif
