using System;
using System.Collections.Generic;
using Gothic.Core.Logging;
using Gothic.Core.Models.Caches;
using Gothic.Core.Services.Caches;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Logger = Gothic.Core.Logging.Logger;

namespace Gothic.Core.Adapters.UI
{
    /// <summary>
    /// An on-screen keyboard in Gothic's font (like SteamVR's) - a panel with the typed text, letters, digits, shift,
    /// space, backspace, OK and cancel, clicked with the UI laser. It shows in front of the camera; a physical keyboard
    /// types into it as well (flat mode, Editor).
    /// Optional: a symbol row (_ - . , : +), forced upper case, a max length and tappable suggestions (e.g. for
    /// cheat commands like "insert ITPO_MANA_03 5" or "goto pos -1200.5 300 40").
    /// Layout lives in the prefab (PrefabType.UiVirtualKeyboard), the optional rows collapse via layout groups.
    /// </summary>
    public class VirtualKeyboard : MonoBehaviour
    {
        public const int DefaultMaxLength = 30;
        private const float _distance = 1.2f;
        private const float _heightBelowEyes = 0.25f;
        private const string _symbols = "_-.,:+";
        // Without the symbol row, the physical keyboard still types these (save names as before).
        private const string _defaultPhysicalSymbols = "-_";

        [SerializeField] private TMP_Text _display;
        [SerializeField] private GameObject _suggestionRow;
        [SerializeField] private Button[] _suggestionButtons;
        [SerializeField] private GameObject _symbolRow;
        [SerializeField] private Button _shiftButton;
        [SerializeField] private Button _spaceButton;
        [SerializeField] private Button _backspaceButton;
        [SerializeField] private Button _submitButton;
        [SerializeField] private Button _cancelButton;

        private static VirtualKeyboard _current;

        private string _text = string.Empty;
        private bool _isUpperCase;
        private int _maxLength = DefaultMaxLength;
        private bool _isForcedUpperCase;
        private bool _isSymbolRowShown;
        private Action<string> _onSubmit;
        private Action _onCancel;
        private Func<string, IReadOnlyList<string>> _suggestionProvider;
        private VirtualKeyboardKey[] _keys;
        private readonly List<string> _suggestions = new();

        /// <param name="initialText">Text already typed when the keyboard opens.</param>
        /// <param name="onSubmit">Called with the trimmed text on OK/Enter.</param>
        /// <param name="onCancel">Called on X/Escape.</param>
        /// <param name="maxLength">Maximum characters (30 for save names, ~64 for cheat commands).</param>
        /// <param name="forceUpperCase">Every letter is typed in upper case (Gothic symbols), shift does nothing.
        /// </param>
        /// <param name="showSymbolRow">Adds a row with _ - . , : + (and lets the physical keyboard type them).</param>
        /// <param name="suggestionProvider">Gets the current text after each change and returns suggestions - the
        /// first ones are shown as buttons between the text and the keys; tapping one replaces the text.</param>
        public static void Show(string initialText, Action<string> onSubmit, Action onCancel,
            int maxLength = DefaultMaxLength, bool forceUpperCase = false, bool showSymbolRow = false,
            Func<string, IReadOnlyList<string>> suggestionProvider = null)
        {
            if (_current != null)
                _current.Close();

            var resourceCacheService = ReflexProjectInstaller.DIContainer.Resolve<ResourceCacheService>();
            var go = resourceCacheService.TryGetPrefabObject(PrefabType.UiVirtualKeyboard);
            _current = go.GetComponent<VirtualKeyboard>();
            _current.Init(initialText ?? string.Empty, onSubmit, onCancel, Mathf.Max(1, maxLength), forceUpperCase,
                showSymbolRow, suggestionProvider);
        }

        private void Init(string initialText, Action<string> onSubmit, Action onCancel, int maxLength,
            bool forceUpperCase, bool showSymbolRow, Func<string, IReadOnlyList<string>> suggestionProvider)
        {
            _onSubmit = onSubmit;
            _onCancel = onCancel;
            _maxLength = maxLength;
            _isForcedUpperCase = forceUpperCase;
            _isUpperCase = forceUpperCase;
            _isSymbolRowShown = showSymbolRow;
            _suggestionProvider = suggestionProvider;
            _text = Clamp(ApplyForcedCase(initialText));

            PlaceInFrontOfCamera();

            foreach (var text in GetComponentsInChildren<TMP_Text>(true))
                GothicText.Apply(text);

            _symbolRow.SetActive(_isSymbolRowShown);
            _suggestionRow.SetActive(_suggestionProvider != null);

            _keys = GetComponentsInChildren<VirtualKeyboardKey>(true);
            foreach (var key in _keys)
            {
                var character = key.Character;
                key.Button.onClick.AddListener(() => Type(character));
            }
            RefreshKeyLabels();

            for (var i = 0; i < _suggestionButtons.Length; i++)
            {
                var index = i;
                _suggestionButtons[i].onClick.AddListener(() => ApplySuggestion(index));
            }

            _shiftButton.onClick.AddListener(ToggleCase);
            _spaceButton.onClick.AddListener(() => Type(' '));
            _backspaceButton.onClick.AddListener(Backspace);
            _submitButton.onClick.AddListener(Submit);
            _cancelButton.onClick.AddListener(Cancel);

            // The optional rows changed - recalculate the panel size before the laser sees it.
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)transform);

            RefreshDisplay();
            GlobalEventDispatcher.UiCanvasShown.Invoke(GetComponent<Canvas>());

            if (Keyboard.current != null)
                Keyboard.current.onTextInput += OnPhysicalTextInput;
            Logger.Log("[VirtualKeyboard] Shown", LogCat.Ui);
        }

        private void PlaceInFrontOfCamera()
        {
            var cam = Camera.main;
            if (cam == null)
                return;

            var forward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.01f)
                forward = cam.transform.up;
            transform.position = cam.transform.position + forward * _distance + Vector3.down * _heightBelowEyes;
            transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
        }

        private void Type(char character)
        {
            if (_text.Length >= _maxLength)
                return;
            _text += char.IsLetter(character) && _isUpperCase ? char.ToUpperInvariant(character) : character;
            RefreshDisplay();
        }

        private void ToggleCase()
        {
            if (_isForcedUpperCase)
                return;
            _isUpperCase = !_isUpperCase;
            RefreshKeyLabels();
        }

        private void RefreshKeyLabels()
        {
            foreach (var key in _keys)
            {
                var character = key.Character;
                if (char.IsLetter(character) && key.Label != null)
                    key.Label.text = (_isUpperCase ? char.ToUpperInvariant(character) : character).ToString();
            }
        }

        private void Backspace()
        {
            if (_text.Length == 0)
                return;
            _text = _text.Substring(0, _text.Length - 1);
            RefreshDisplay();
        }

        private void ApplySuggestion(int index)
        {
            if (index < 0 || index >= _suggestions.Count)
                return;
            _text = Clamp(ApplyForcedCase(_suggestions[index]));
            RefreshDisplay();
        }

        private void RefreshDisplay()
        {
            _display.text = _text + "_";
            RefreshSuggestions();
        }

        private void RefreshSuggestions()
        {
            if (_suggestionProvider == null)
                return;

            _suggestions.Clear();
            try
            {
                var suggestions = _suggestionProvider(_text);
                if (suggestions != null)
                {
                    foreach (var suggestion in suggestions)
                    {
                        if (_suggestions.Count >= _suggestionButtons.Length)
                            break;
                        if (!string.IsNullOrEmpty(suggestion))
                            _suggestions.Add(suggestion);
                    }
                }
            }
            catch (Exception e)
            {
                Logger.LogWarning($"[VirtualKeyboard] Suggestion provider failed: {e.Message}", LogCat.Ui);
            }

            for (var i = 0; i < _suggestionButtons.Length; i++)
            {
                var isUsed = i < _suggestions.Count;
                // Unused buttons are hidden, the row keeps its height (LayoutElement on the row).
                _suggestionButtons[i].gameObject.SetActive(isUsed);
                if (isUsed)
                    _suggestionButtons[i].GetComponentInChildren<TMP_Text>(true).text = _suggestions[i];
            }
        }

        private void Submit()
        {
            var text = _text.Trim();
            var onSubmit = _onSubmit;
            Logger.Log($"[VirtualKeyboard] Submitted '{text}'", LogCat.Ui);
            Close();
            onSubmit?.Invoke(text);
        }

        private void Cancel()
        {
            var onCancel = _onCancel;
            Logger.Log("[VirtualKeyboard] Cancelled", LogCat.Ui);
            Close();
            onCancel?.Invoke();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;
            if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
                Submit();
            else if (keyboard.escapeKey.wasPressedThisFrame)
                Cancel();
            else if (keyboard.backspaceKey.wasPressedThisFrame)
                Backspace();
        }

        private void OnPhysicalTextInput(char character)
        {
            var symbols = _isSymbolRowShown ? _symbols : _defaultPhysicalSymbols;
            if (!char.IsLetterOrDigit(character) && character != ' ' && symbols.IndexOf(character) < 0)
                return;
            if (_text.Length >= _maxLength)
                return;

            _text += _isForcedUpperCase ? char.ToUpperInvariant(character) : character;
            RefreshDisplay();
        }

        private string ApplyForcedCase(string text)
        {
            return _isForcedUpperCase ? text.ToUpperInvariant() : text;
        }

        private string Clamp(string text)
        {
            return text.Length > _maxLength ? text.Substring(0, _maxLength) : text;
        }

        private void Close()
        {
            if (Keyboard.current != null)
                Keyboard.current.onTextInput -= OnPhysicalTextInput;
            GlobalEventDispatcher.UiCanvasHidden.Invoke(GetComponent<Canvas>());
            if (_current == this)
                _current = null;
            _onSubmit = null;
            _onCancel = null;
            _suggestionProvider = null;
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (Keyboard.current != null)
                Keyboard.current.onTextInput -= OnPhysicalTextInput;
            if (_current == this)
                _current = null;
        }
    }
}
