using System;
using System.Collections;
using Gothic.Core.Adapters.Npc;
using Gothic.Core.Logging;
using Gothic.Core.Models.Container;
using Gothic.Core.Services.Meshes;
using Gothic.Core.Services.Npc;
using Gothic.Core.Services.Player;
using Reflex.Attributes;
using UnityEngine;
using UnityEngine.UI;
using ZenKit.Daedalus;
using Logger = Gothic.Core.Logging.Logger;

namespace Gothic.Core.Adapters.UI.StatusBars
{
    public class StatusBarAdapter : MonoBehaviour
    {
        [SerializeField] private StatusType _statusType;
        [SerializeField] private bool _isPlayer;
        [SerializeField] private Image _background;
        [SerializeField] private Image _statusValue;

        [Inject] private readonly TextureService _textureService;
        [Inject] private readonly PlayerService _playerService;
        [Inject] private readonly NpcService _npcService;

        // NPC bars are nested below the NPC's prefab. Wait this many frames for it before warning.
        private const int _ownerWarningFrames = 300;

        private NpcContainer _owner;
        private int _lastHitPoints = int.MinValue;
        private int _lastHitPointsMax = int.MinValue;
        private int _ownerWaitFrames;

        private enum StatusType
        {
            Health,
            Mana,
            Misc
        }

        private void Awake()
        {
            // Player is spawned before ZenKit (with Gothic version) is bootstrapped.
            if (_isPlayer)
            {
                // TODO - Keep health bar always active as in G1, potentially only enabling it when a weapon is drawn in the future (immersion update).
                if (_statusType != StatusType .Health)
                {
                    DisableBar();
                }

                // If we load it for the player, ZenKit is initialized later therefore, we need to delay the startup execution.
                GlobalEventDispatcher.ZenKitBootstrapped.AddListener(StartInternal);
            }
            else
            {
                // Same exception as the player above: Health stays visible. Nothing re-enables a disabled NPC
                // health bar later on.
                if (_statusType != StatusType.Health)
                {
                    DisableBar();
                }
                StartInternal();
            }
        }

        private void StartInternal()
        {
            _background.material = _textureService.StatusBarBackgroundMaterial;

            // FIXME - Set fill amount based on current value when NPC/Monster is spawned.
            _statusValue.fillAmount = 1f;
            _statusValue.material = _statusType switch
            {
                StatusType.Health => _textureService.StatusBarHealthMaterial,
                StatusType.Mana => _textureService.StatusBarManaMaterial,
                StatusType.Misc => _textureService.StatusBarMiscMaterial,
                _ => throw new ArgumentOutOfRangeException()
            };

            switch (_statusType)
            {
                case StatusType.Health:
                    if (_isPlayer)
                        DisableBar();
                    break;
                case StatusType.Mana:
                    break;
                case StatusType.Misc:
                    StartCoroutine(HandleDiveValue());
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        /// <summary>
        /// Health uses Update() instead of a coroutine (unlike HandleDiveValue) because NPCs get disabled/re-enabled
        /// by culling - a coroutine's iterator dies on disable and never resumes on its own, freezing the bar forever.
        /// </summary>
        private void Update()
        {
            if (_statusType != StatusType.Health)
                return;

            if (_owner == null)
            {
                // The player's bar isn't below an NPC prefab (VR rig) - it asks for the hero directly.
                // The hero doesn't exist yet in menus, so this polls until it does.
                _owner = _isPlayer ? _npcService.GetHeroContainer() : GetComponentInParent<NpcLoader>()?.Container;
                if (_owner == null)
                {
                    _ownerWaitFrames++;
                    if (!_isPlayer && _ownerWaitFrames == _ownerWarningFrames)
                        Logger.LogWarning($"[StatusBarAdapter] '{name}' has no NpcLoader owner after " +
                                          $"{_ownerWaitFrames} frames. Is the bar placed below the NPC?", LogCat.Ui);
                    return;
                }
            }

            var hp = _owner.Vob.GetAttribute((int)NpcAttribute.HitPoints);
            var hpMax = _owner.Vob.GetAttribute((int)NpcAttribute.HitPointsMax);
            if (hp != _lastHitPoints || hpMax != _lastHitPointsMax)
            {
                _lastHitPoints = hp;
                _lastHitPointsMax = hpMax;
                SetFillAmount(hp, hpMax);
            }
        }

        private IEnumerator HandleDiveValue()
        {
            while (true)
            {
                if (!_playerService.IsDiving && _statusValue.enabled)
                {
                    DisableBar();
                }
                else if (_playerService.IsDiving)
                {
                    SetFillAmount(_playerService.CurrentAir, _playerService.MaxAir);
                    
                    if (!_statusValue.enabled)
                        EnableBar();
                }

                yield return null;
            }
            // ReSharper disable once IteratorNeverReturns
        }

        public void SetFillAmount(float current, float max)
        {
            _statusValue.fillAmount = current / max;
        }

        public void DisableBar()
        {
            _background.enabled = false;
            _statusValue.enabled = false;
        }

        public void EnableBar()
        {
            _background.enabled = true;
            _statusValue.enabled = true;
        }
    }
}
