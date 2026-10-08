using System;
using System.Collections.Generic;
using Gothic.Core.Creator;
using Gothic.Core.Logging;
using Gothic.Core.Models.Caches;
using Gothic.Core.Models.Vm;
using Gothic.Core.Services.Caches;
using Gothic.Core.Services.Config;
using Gothic.Core.Services.Context;
using Gothic.Core.Services.Npc;
using Gothic.Core.Services.Player;
using Gothic.Core.Services.World;
using Reflex.Attributes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ZenKit.Daedalus;
using Logger = Gothic.Core.Logging.Logger;

namespace Gothic.VR.Adapters.Marvin
{
    /// <summary>
    /// Marvin tab with developer cheat buttons. Each button is shown only when its DeveloperConfig entry is set.
    /// </summary>
    public class MarvinTabHandler : MonoBehaviour
    {
        [Inject] private readonly ConfigService _configService;
        [Inject] private readonly ContextInteractionService _contextInteractionService;
        [Inject] private readonly GameTimeService _gameTimeService;
        [Inject] private readonly NpcService _npcService;
        [Inject] private readonly PlayerService _playerService;
        [Inject] private readonly ResourceCacheService _resourceCacheService;
        [Inject] private readonly VmCacheService _vmCacheService;
        [Inject] private readonly WayNetService _wayNetService;

        private const float _buttonHeight = 50f;
        private const float _buttonGap = 10f;
        private const int _levelsPerLevelCheat = 5;
        private const int _manaMaxPerLevelCheat = 100;


        private void Start()
        {
            var placeholder = transform.Find("Text");
            if (placeholder != null)
                placeholder.gameObject.SetActive(false);

            CreateButtons();
        }

        private void CreateButtons()
        {
            var dev = _configService.Dev;
            var buttons = new List<(string label, Action onClick)>();

            if (dev.EnableLevel5Cheat)
                buttons.Add(($"Level +{_levelsPerLevelCheat}", CheatAddLevels));

            if (dev.EnableGuildCheat)
                buttons.Add(("Guild → Novice", CheatToNovice));

            if (dev.EnableScavengerGuildCheat)
                buttons.Add(("Guild → Scavenger", CheatToScavenger));

            if (dev.EnableTimeSkip)
                buttons.Add(("Skip Time +30min", SkipTime30Min));

            if (!string.IsNullOrEmpty(dev.MarvinTeleportWaypoint))
                buttons.Add(($"TP → {dev.MarvinTeleportWaypoint}", CheatTeleportToWaypoint));

            if (!string.IsNullOrEmpty(dev.MarvinSpawnNpcSymbol))
                buttons.Add(($"Spawn {dev.MarvinSpawnNpcSymbol}", CheatSpawnNpc));

            if (!string.IsNullOrEmpty(dev.MarvinGiveItems))
                buttons.Add(("Give items", CheatGiveItems));

            // Vertically centered column of buttons.
            var totalHeight = buttons.Count * _buttonHeight + (buttons.Count - 1) * _buttonGap;
            var startY = totalHeight / 2f - _buttonHeight / 2f;

            for (var i = 0; i < buttons.Count; i++)
            {
                var (label, onClick) = buttons[i];
                CreateButton(label, onClick, startY - i * (_buttonHeight + _buttonGap));
            }
        }

        private void CheatAddLevels()
        {
            var hero = _npcService.GetHeroContainer();
            var oldLevel = hero.Instance.Level;
            hero.Instance.Level += _levelsPerLevelCheat;
            hero.Instance.Lp += _levelsPerLevelCheat * 10;

            // HP and mana max up, both filled. Set on the instance: SyncHeroInstanceToVob() copies the instance into
            // the vob, so vob-only values would be reverted at once.
            var hpMax = hero.Instance.GetAttribute(NpcAttribute.HitPointsMax) + _levelsPerLevelCheat * 12;
            var manaMax = hero.Instance.GetAttribute(NpcAttribute.ManaMax) + _manaMaxPerLevelCheat;
            hero.Instance.SetAttribute(NpcAttribute.HitPointsMax, hpMax);
            hero.Instance.SetAttribute(NpcAttribute.HitPoints, hpMax);
            hero.Instance.SetAttribute(NpcAttribute.ManaMax, manaMax);
            hero.Instance.SetAttribute(NpcAttribute.Mana, manaMax);
            _npcService.SyncHeroInstanceToVob();

            Logger.Log($"[MarvinMode] Level cheat: {oldLevel}→{hero.Instance.Level} " +
                       $"(+{_levelsPerLevelCheat * 10} LP, HP {hpMax}/{hpMax}, mana {manaMax}/{manaMax})", LogCat.Ui);
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
            Logger.Log("[MarvinMode] Guild cheat: set to GIL_SCAVENGER", LogCat.Ui);
        }

        private void SkipTime30Min()
        {
            // Total hours, so that a skip over midnight moves to the next day (SetTime() accepts hours > 23).
            // NPC routines follow with the next game minute tick (NpcRoutineService.OnGameTimeMinuteChanged).
            var next = _gameTimeService.GetCurrentTime().Add(TimeSpan.FromMinutes(30));
            _gameTimeService.SetTime((int)next.TotalHours, next.Minutes);
            Logger.Log($"[MarvinMode] Time skip → {next.Hours:D2}:{next.Minutes:D2}", LogCat.Ui);
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

        private void CheatSpawnNpc()
        {
            var symbol = _configService.Dev.MarvinSpawnNpcSymbol;
            var heroGo = _npcService.GetHeroContainer()?.Go;
            if (heroGo == null)
            {
                Logger.LogWarning("[MarvinMode] Spawn NPC: hero GameObject not found", LogCat.Ui);
                return;
            }

            var position = heroGo.transform.position + heroGo.transform.forward * 2f;
            if (_npcService.SpawnNpcByName(symbol, position, heroGo.transform.rotation))
                Logger.Log($"[MarvinMode] Spawned '{symbol}' near player", LogCat.Ui);
        }

        /// <summary>
        /// DeveloperConfig.MarvinGiveItems, e.g. "ITRU_FIREBOLT, ITPO_MANA_03:5", goes into the hero's inventory.
        /// </summary>
        private void CheatGiveItems()
        {
            foreach (var entry in _configService.Dev.MarvinGiveItems.Split(',', ';', '\n'))
            {
                var parts = entry.Trim().Split(':');
                var itemName = parts[0].Trim().ToUpper();
                if (itemName.Length == 0)
                    continue;

                var amount = 1;
                if (parts.Length > 1 && int.TryParse(parts[1].Trim(), out var parsedAmount))
                    amount = Mathf.Max(1, parsedAmount);

                if (_vmCacheService.TryGetItemData(itemName) == null)
                {
                    Logger.LogWarning($"[MarvinMode] Give items: unknown item '{itemName}'", LogCat.Ui);
                    continue;
                }

                _playerService.AddItem(itemName, amount);
                Logger.Log($"[MarvinMode] Gave {itemName} x{amount}", LogCat.Ui);
            }
        }

        private void CreateButton(string label, Action onClick, float anchoredY)
        {
            var go = _resourceCacheService.TryGetPrefabObject(PrefabType.UiDebugButton, parent: gameObject);
            if (go == null)
            {
                Logger.LogWarning($"[MarvinMode] UiDebugButton prefab not found for: {label}", LogCat.Ui);
                return;
            }

            var rectTransform = go.GetComponent<RectTransform>();
            if (rectTransform != null)
                rectTransform.anchoredPosition = new Vector2(0f, anchoredY);

            var text = go.GetComponentInChildren<TMP_Text>();
            if (text != null)
                text.text = label;

            var button = go.GetComponentInChildren<Button>();
            if (button != null)
                button.onClick.AddListener(() => onClick());
        }
    }
}
