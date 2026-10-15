using System;
using System.Collections.Generic;
using Gothic.Core;
using Gothic.Core.Adapters.UI;
using Gothic.Core.Logging;
using Gothic.Core.Models.UI;
using Gothic.Core.Services;
using Gothic.Core.Services.Marvin;
using Reflex.Attributes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Logger = Gothic.Core.Logging.Logger;

namespace Gothic.VR.Adapters.Marvin
{
    /// <summary>
    /// Marvin tab: build a command line (recent commands, tappable completions, VirtualKeyboard next to the canvas)
    /// and run it. Layout lives in the prefab; the logic of the commands lives in MarvinCommandService.
    /// </summary>
    public class MarvinTabHandler : MonoBehaviour
    {
        [SerializeField] private TMP_Text _commandLine;
        [SerializeField] private Button _backButton;
        [SerializeField] private Button _clearButton;
        [SerializeField] private Button _runButton;
        [SerializeField] private Button _keyboardButton;
        [SerializeField] private Button[] _recentButtons;
        [SerializeField] private Button[] _optionButtons;
        [SerializeField] private Button _previousPageButton;
        [SerializeField] private Button _nextPageButton;
        [SerializeField] private TMP_Text _pageLabel;
        [SerializeField] private TMP_Text _optionsHint;
        [SerializeField, Tooltip("Where the keyboard stands (below the canvas; position, rotation, scale).")]
        private Transform _keyboardAnchor;

        [Inject] private readonly MarvinCommandService _marvinCommandService;
        [Inject] private readonly GameStateService _gameStateService;

        private const int _commandMaxLength = 64;
        private const string _placeholderColor = "#808080";

        private readonly List<string> _shownOptions = new();
        private readonly List<string> _shownRecents = new();

        private string _line = string.Empty;
        private int _page;
        private VirtualKeyboard _keyboard;

        private void Awake()
        {
            _backButton.onClick.AddListener(Back);
            _clearButton.onClick.AddListener(Clear);
            _runButton.onClick.AddListener(Run);
            _keyboardButton.onClick.AddListener(OpenKeyboard);
            _previousPageButton.onClick.AddListener(() => ChangePage(-1));
            _nextPageButton.onClick.AddListener(() => ChangePage(1));

            for (var i = 0; i < _recentButtons.Length; i++)
            {
                var index = i;
                _recentButtons[i].onClick.AddListener(() => RunRecent(index));
            }

            for (var i = 0; i < _optionButtons.Length; i++)
            {
                var index = i;
                _optionButtons[i].onClick.AddListener(() => ApplyOption(index));
            }
        }

        private void OnEnable()
        {
            GlobalEventDispatcher.MarvinRecentCommandsChanged.AddListener(RefreshRecents);

            OpenKeyboard();
            Refresh();
            RefreshRecents();
        }

        private void OnDisable()
        {
            GlobalEventDispatcher.MarvinRecentCommandsChanged.RemoveListener(RefreshRecents);

            // Unity null check: another keyboard (e.g. save name) already replaced ours.
            if (_keyboard != null)
                _keyboard.Close();
            _keyboard = null;
        }

        private bool IsReady()
        {
            // The Daedalus VM isn't loaded before a game starts - the service can't resolve symbols then.
            return _gameStateService.GothicVm != null;
        }

        private void OpenKeyboard()
        {
            if (!IsReady() || _keyboard != null)
                return;

            _keyboard = VirtualKeyboard.Show(new VirtualKeyboardOptions
            {
                InitialText = _line,
                MaxLength = _commandMaxLength,
                IsForcedUpperCase = true,
                IsSymbolRowShown = true,
                IsClosedOnSubmit = false,
                FollowTarget = _keyboardAnchor,
                KeyFilter = (text, c) => _marvinCommandService.IsAllowedNextChar(text, c),
                CanSubmit = text => _marvinCommandService.CanExecute(text),
                OnTextChanged = OnKeyboardTextChanged,
                OnSubmit = text =>
                {
                    SetLine(text, false);
                    Run();
                },
                OnCancel = () => _keyboard = null
            });
        }

        /// <summary>
        /// Typed chars get auto-completed, deleted ones revert to the last point with a choice. The keyboard gets the
        /// result in the same frame (its OnTextChanged callback), so the intermediate text is never rendered.
        /// </summary>
        private void OnKeyboardTextChanged(string text)
        {
            text ??= string.Empty;
            var line = text;
            if (text.Length > _line.Length && text.StartsWith(_line, StringComparison.OrdinalIgnoreCase))
                line = _marvinCommandService.AutoComplete(text);
            else if (text.Length < _line.Length)
                line = _marvinCommandService.RevertToLastBranch(text);

            SetLine(line, line != text);
        }

        /// <summary>
        /// Single source of truth for the command line. Typing on the keyboard already knows the text itself.
        /// Upper-case like the keyboard (forced), comparisons in the service ignore the case anyway.
        /// </summary>
        private void SetLine(string line, bool isKeyboardUpdated)
        {
            _line = (line ?? string.Empty).ToUpperInvariant();
            _page = 0;

            if (isKeyboardUpdated && _keyboard != null)
                _keyboard.SetText(_line);
            Refresh();
        }

        private void Back()
        {
            if (IsReady())
                SetLine(_marvinCommandService.RemoveLastToken(_line), true);
        }

        private void Clear()
        {
            SetLine(string.Empty, true);
        }

        private void Run()
        {
            if (!IsReady() || !_marvinCommandService.CanExecute(_line))
                return;

            // A failed command keeps the line, so it can be fixed.
            if (_marvinCommandService.Execute(_line))
                SetLine(string.Empty, true);
            else
                Logger.LogWarning($"[MarvinTabHandler] '{_line}' failed.", LogCat.Ui);
        }

        private void RunRecent(int index)
        {
            if (!IsReady() || index >= _shownRecents.Count)
                return;

            // Fills the line first, so the keyboard and the command line show what was executed.
            SetLine(_shownRecents[index], true);
            Run();
        }

        private void ApplyOption(int index)
        {
            if (!IsReady() || index >= _shownOptions.Count)
                return;

            SetLine(_marvinCommandService.ApplyOption(_line, _shownOptions[index]), true);
        }

        private void ChangePage(int direction)
        {
            _page = Mathf.Max(0, _page + direction);
            Refresh();
        }

        private void RefreshRecents()
        {
            _shownRecents.Clear();
            if (IsReady())
                _shownRecents.AddRange(_marvinCommandService.RecentCommands);

            for (var i = 0; i < _recentButtons.Length; i++)
            {
                var isUsed = i < _shownRecents.Count;
                _recentButtons[i].gameObject.SetActive(isUsed);
                if (isUsed)
                    _recentButtons[i].GetComponentInChildren<TMP_Text>(true).text = _shownRecents[i];
            }
        }

        private void Refresh()
        {
            var isReady = IsReady();
            var placeholder = isReady ? _marvinCommandService.GetPlaceholder(_line) : null;
            var total = 0;

            _shownOptions.Clear();
            if (isReady)
            {
                var count = _optionButtons.Length;
                _shownOptions.AddRange(_marvinCommandService.GetOptions(_line, _page * count, count, out total));

                // Line got shorter while a later page was shown.
                if (_shownOptions.Count == 0 && _page > 0 && total > 0)
                {
                    _page = (total - 1) / count;
                    _shownOptions.AddRange(_marvinCommandService.GetOptions(_line, _page * count, count, out total));
                }
            }

            RefreshCommandLine(placeholder);
            RefreshOptions(placeholder, total);

            _backButton.interactable = isReady && _line.Length > 0;
            _clearButton.interactable = isReady && _line.Length > 0;
            _runButton.interactable = isReady && _marvinCommandService.CanExecute(_line);
            _keyboardButton.interactable = isReady;
        }

        private void RefreshCommandLine(string placeholder)
        {
            if (!IsReady())
            {
                _commandLine.text = "Load a game first.";
                return;
            }

            // The placeholder only stands for a value not typed yet.
            var isValueStarted = _line.Length > 0 && !_line.EndsWith(" ");
            if (placeholder == null || isValueStarted)
            {
                _commandLine.text = _line + "_";
                return;
            }

            // <noparse>: the placeholder looks like a rich text tag.
            _commandLine.text = $"{_line}<color={_placeholderColor}><noparse>{placeholder}</noparse></color>";
        }

        private void RefreshOptions(string placeholder, int total)
        {
            var count = _optionButtons.Length;
            var pageCount = Mathf.Max(1, (total + count - 1) / count);
            var isHintShown = placeholder != null && total == 0;

            _optionsHint.gameObject.SetActive(isHintShown);
            if (isHintShown)
                _optionsHint.text = _marvinCommandService.CanExecute(_line)
                    ? $"Run, or type <noparse>{placeholder}</noparse> on the keyboard"
                    : $"Type <noparse>{placeholder}</noparse> on the keyboard";

            for (var i = 0; i < _optionButtons.Length; i++)
            {
                var isUsed = i < _shownOptions.Count;
                _optionButtons[i].gameObject.SetActive(isUsed);
                if (isUsed)
                    _optionButtons[i].GetComponentInChildren<TMP_Text>(true).text = _shownOptions[i];
            }

            _pageLabel.text = total > 0 ? $"{_page + 1}/{pageCount} ({total})" : string.Empty;
            _previousPageButton.interactable = _page > 0;
            _nextPageButton.interactable = _page < pageCount - 1;
        }
    }
}
