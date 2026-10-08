using System;
using System.Collections.Generic;
using Gothic.Core.Creator;
using Gothic.Core.Logging;
using Gothic.Core.Models.Container;
using Gothic.Core.Models.Caches;
using Gothic.Core.Services;
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
        [Inject] private readonly GameStateService _gameStateService;
        [Inject] private readonly GameTimeService _gameTimeService;
        [Inject] private readonly NpcService _npcService;
        [Inject] private readonly PlayerService _playerService;
        [Inject] private readonly ResourceCacheService _resourceCacheService;
        [Inject] private readonly VmCacheService _vmCacheService;
        [Inject] private readonly WayNetService _wayNetService;

        private const float _buttonHeight = 50f;
        private const float _buttonGap = 10f;
        private const int _levelsPerLevelCheat = 5;
        private const int _learnPointsPerLevel = 10;
        private const int _hitPointsMaxPerLevel = 12;
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
                buttons.Add(("Guild -> Novice", () => CheatSetGuild("GIL_NOV")));

            if (dev.EnableScavengerGuildCheat)
                buttons.Add(("Guild -> Scavenger", () => CheatSetGuild("GIL_SCAVENGER")));

            if (dev.EnableTimeSkip)
                buttons.Add(("Skip Time +30min", SkipTime30Min));

            if (!string.IsNullOrEmpty(dev.MarvinTeleportWaypoint))
                buttons.Add(($"TP -> {dev.MarvinTeleportWaypoint}", CheatTeleportToWaypoint));

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

        /// <summary>
        /// Guild ids differ between G1 and G2 (e.g. GIL_NOV), so the value is read from the scripts' constant.
        /// Resolved on click: the Daedalus VM isn't loaded yet when the Marvin menu starts.
        /// Sets guild and true guild like the scripts do when the hero joins (Npc_SetTrueGuild + hero.guild).
        /// </summary>
        private void CheatSetGuild(string guildSymbolName)
        {
            var guildSymbol = _gameStateService.GothicVm?.GetSymbolByName(guildSymbolName);
            if (guildSymbol == null)
            {
                Logger.LogWarning($"[MarvinTabHandler] Guild cheat: {guildSymbolName} isn't defined by the scripts.",
                    LogCat.Ui);
                return;
            }

            var guild = guildSymbol.GetInt(0);
            var hero = _npcService.GetHeroContainer();
            hero.Instance.Guild = guild;
            hero.Vob.Guild = guild;
            hero.Vob.GuildTrue = guild;
            Logger.Log($"[MarvinTabHandler] Guild cheat: hero guild set to {guildSymbolName} ({guild})", LogCat.Ui);
        }

        /// <summary>
        /// Writes instance and vob for each value it changes: scripts read the instance, the game (status menu,
        /// Npc_ChangeAttribute, fights) the vob. A full instance -> vob sync would revert vob-only changes
        /// like eaten plants or potions.
        /// </summary>
        private void CheatAddLevels()
        {
            var hero = _npcService.GetHeroContainer();
            var oldLevel = hero.Vob.Level;

            hero.Vob.Level = hero.Instance.Level = oldLevel + _levelsPerLevelCheat;
            hero.Vob.Lp = hero.Instance.Lp = hero.Vob.Lp + _levelsPerLevelCheat * _learnPointsPerLevel;

            var hpMax = GetHeroAttribute(hero, NpcAttribute.HitPointsMax)
                        + _levelsPerLevelCheat * _hitPointsMaxPerLevel;
            var manaMax = GetHeroAttribute(hero, NpcAttribute.ManaMax) + _manaMaxPerLevelCheat;
            SetHeroAttribute(hero, NpcAttribute.HitPointsMax, hpMax);
            SetHeroAttribute(hero, NpcAttribute.HitPoints, hpMax);
            SetHeroAttribute(hero, NpcAttribute.ManaMax, manaMax);
            SetHeroAttribute(hero, NpcAttribute.Mana, manaMax);

            Logger.Log($"[MarvinTabHandler] Level cheat: {oldLevel}->{hero.Vob.Level} (LP {hero.Vob.Lp}, " +
                       $"HP {hpMax}/{hpMax}, mana {manaMax}/{manaMax})", LogCat.Ui);
        }

        private static int GetHeroAttribute(NpcContainer hero, NpcAttribute attribute)
        {
            return hero.Vob.GetAttribute((int)attribute);
        }

        private static void SetHeroAttribute(NpcContainer hero, NpcAttribute attribute, int value)
        {
            hero.Vob.SetAttribute((int)attribute, value);
            hero.Instance.SetAttribute(attribute, value);
        }

        private void SkipTime30Min()
        {
            // Total hours, so that a skip over midnight moves to the next day (SetTime() accepts hours > 23).
            // NPC routines follow with the next game minute tick (NpcRoutineService.OnGameTimeMinuteChanged).
            var next = _gameTimeService.GetCurrentTime().Add(TimeSpan.FromMinutes(30));
            _gameTimeService.SetTime((int)next.TotalHours, next.Minutes);
            Logger.Log($"[MarvinTabHandler] Time skip -> {next.Hours:D2}:{next.Minutes:D2}", LogCat.Ui);
        }

        private void CheatTeleportToWaypoint()
        {
            var wpName = _configService.Dev.MarvinTeleportWaypoint;
            var wp = _wayNetService.GetWayNetPoint(wpName);
            if (wp == null)
            {
                Logger.LogWarning($"[MarvinTabHandler] Teleport WP '{wpName}' not found", LogCat.Ui);
                return;
            }

            _contextInteractionService.TeleportPlayerTo(wp.Position, wp.Rotation);
            Logger.Log($"[MarvinTabHandler] Teleported to '{wpName}'", LogCat.Ui);
        }

        private void CheatSpawnNpc()
        {
            var symbol = _configService.Dev.MarvinSpawnNpcSymbol;
            var heroGo = _npcService.GetHeroContainer()?.Go;
            if (heroGo == null)
            {
                Logger.LogWarning("[MarvinTabHandler] Spawn NPC: hero GameObject not found", LogCat.Ui);
                return;
            }

            var position = heroGo.transform.position + heroGo.transform.forward * 2f;
            if (_npcService.SpawnNpcByName(symbol, position, heroGo.transform.rotation))
                Logger.Log($"[MarvinTabHandler] Spawned '{symbol}' near player", LogCat.Ui);
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
                    Logger.LogWarning($"[MarvinTabHandler] Give items: unknown item '{itemName}'", LogCat.Ui);
                    continue;
                }

                _playerService.AddItem(itemName, amount);
                Logger.Log($"[MarvinTabHandler] Gave {itemName} x{amount}", LogCat.Ui);
            }
        }

        private void CreateButton(string label, Action onClick, float anchoredY)
        {
            var go = _resourceCacheService.TryGetPrefabObject(PrefabType.UiDebugButton, parent: gameObject);
            if (go == null)
            {
                Logger.LogWarning($"[MarvinTabHandler] UiDebugButton prefab not found for: {label}", LogCat.Ui);
                return;
            }

            go.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, anchoredY);
            go.GetComponentInChildren<TMP_Text>().text = label;
            go.GetComponentInChildren<Button>().onClick.AddListener(() => onClick());
        }
    }
}
