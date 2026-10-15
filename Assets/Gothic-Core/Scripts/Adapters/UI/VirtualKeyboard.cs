using System;
using System.Collections.Generic;
using Gothic.Core.Logging;
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
    /// </summary>
    public class VirtualKeyboard : MonoBehaviour
    {
        public const int DefaultMaxLength = 30;
        private const int _maxSuggestions = 5;
        private const float _distance = 1.2f;
        private const float _heightBelowEyes = 0.25f;
        private const float _pixelSize = 0.001f;
        private const float _keyWidth = 70f;
        private const float _keyHeight = 60f;
        private const float _keySpacing = 8f;
        private const float _fontSize = 34f;
        private const float _suggestionFontSize = 24f;
        private const float _suggestionMinFontSize = 12f;

        private static readonly string[] _rows = { "1234567890", "qwertyuiop", "asdfghjkl", "zxcvbnm" };
        private const string _symbolRow = "_-.,:+";
        // Without the symbol row, the physical keyboard still types these (save names as before).
        private const string _defaultPhysicalSymbols = "-_";
        private static readonly Color _panelColor = new(0.05f, 0.04f, 0.03f, 0.92f);
        private static readonly Color _keyColor = new(0.25f, 0.21f, 0.16f, 0.95f);
        private static readonly Color _actionKeyColor = new(0.35f, 0.27f, 0.15f, 0.95f);
        private static readonly Color _suggestionColor = new(0.18f, 0.24f, 0.3f, 0.95f);

        private static VirtualKeyboard _current;

        private TMP_Text _display;
        private string _text = string.Empty;
        private bool _isUpperCase;
        private int _maxLength = DefaultMaxLength;
        private bool _isForcedUpperCase;
        private bool _isSymbolRowShown;
        private Action<string> _onSubmit;
        private Action _onCancel;
        private Func<string, IReadOnlyList<string>> _suggestionProvider;
        private readonly List<(TMP_Text Label, char Letter)> _letterKeys = new();
        private readonly List<(GameObject Key, TMP_Text Label)> _suggestionKeys = new();
        private readonly List<string> _suggestions = new();

        /// <param name="initialText">Text already typed when the keyboard opens.</param>
        /// <param name="onSubmit">Called with the trimmed text on OK/Enter.</param>
        /// <param name="onCancel">Called on X/Escape.</param>
        /// <param name="maxLength">Maximum characters (30 for save names, ~64 for cheat commands).</param>
        /// <param name="forceUpperCase">Every letter is typed in upper case (Gothic symbols), shift does nothing.
        /// </param>
        /// <param name="showSymbolRow">Adds a row with _ - . , : + (and lets the physical keyboard type them).</param>
        /// <param name="suggestionProvider">Gets the current text after each change and returns suggestions - up to
        /// 5 are shown as buttons between the text and the keys; tapping one replaces the text.</param>
        public static void Show(string initialText, Action<string> onSubmit, Action onCancel,
            int maxLength = DefaultMaxLength, bool forceUpperCase = false, bool showSymbolRow = false,
            Func<string, IReadOnlyList<string>> suggestionProvider = null)
        {
            if (_current != null)
                _current.Close();

            var go = new GameObject("VirtualKeyboard", typeof(RectTransform));
            _current = go.AddComponent<VirtualKeyboard>();
            _current._maxLength = Mathf.Max(1, maxLength);
            _current._isForcedUpperCase = forceUpperCase;
            _current._isUpperCase = forceUpperCase;
            _current._isSymbolRowShown = showSymbolRow;
            _current._suggestionProvider = suggestionProvider;
            _current.Build(initialText ?? string.Empty, onSubmit, onCancel);
        }

        private void Build(string initialText, Action<string> onSubmit, Action onCancel)
        {
            _onSubmit = onSubmit;
            _onCancel = onCancel;
            _text = Clamp(ApplyForcedCase(initialText));

            PlaceInFrontOfCamera();

            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 100;
            gameObject.AddComponent<GraphicRaycaster>();

            // Rows: display, [suggestions], [symbols], 4 key rows, bottom row - the panel grows with the optional ones.
            var unit = _keyHeight + _keySpacing;
            var rowCount = 1 + (_suggestionProvider != null ? 1 : 0) + (_isSymbolRowShown ? 1 : 0) + _rows.Length + 1;
            var width = 10 * (_keyWidth + _keySpacing) + 3 * _keySpacing;
            var height = rowCount * unit + 3 * _keySpacing;
            ((RectTransform)transform).sizeDelta = new Vector2(width, height);
            transform.localScale = Vector3.one * _pixelSize;

            var panel = gameObject.AddComponent<Image>();
            panel.color = _panelColor;

            // The typed text on top, then the rows - each row centered.
            var top = height / 2f - _keySpacing * 2f - _keyHeight / 2f;
            var innerWidth = width - 4f * _keySpacing;
            _display = CreateLabel(transform, new Vector2(0f, top), new Vector2(innerWidth, _keyHeight));
            var displayBackground = _display.transform.parent.GetComponent<Image>();
            displayBackground.color = new Color(0f, 0f, 0f, 0.6f);

            var rowIndex = 1;
            if (_suggestionProvider != null)
                CreateSuggestionRow(top - rowIndex++ * unit, innerWidth);

            if (_isSymbolRowShown)
                CreateCharacterRow(_symbolRow, top - rowIndex++ * unit);

            foreach (var keys in _rows)
                CreateCharacterRow(keys, top - rowIndex++ * unit);

            // Bottom row: shift, space, backspace, OK, cancel.
            var bottom = top - rowIndex * unit;
            var keyUnit = _keyWidth + _keySpacing;
            CreateKey(new Vector2(-4f * keyUnit, bottom), new Vector2(keyUnit * 1.5f - _keySpacing, _keyHeight), "Aa",
                _actionKeyColor, ToggleCase);
            CreateKey(new Vector2(-1.25f * keyUnit, bottom), new Vector2(keyUnit * 3.5f - _keySpacing, _keyHeight),
                "Space", _keyColor, () => Type(' '));
            CreateKey(new Vector2(1.5f * keyUnit, bottom), new Vector2(keyUnit * 1.5f - _keySpacing, _keyHeight), "<-",
                _actionKeyColor, Backspace);
            CreateKey(new Vector2(3f * keyUnit, bottom), new Vector2(keyUnit * 1.5f - _keySpacing, _keyHeight), "OK",
                new Color(0.2f, 0.45f, 0.2f, 0.95f), Submit);
            CreateKey(new Vector2(4.25f * keyUnit, bottom), new Vector2(keyUnit - _keySpacing, _keyHeight), "X",
                new Color(0.5f, 0.2f, 0.2f, 0.95f), Cancel);

            RefreshDisplay();
            GlobalEventDispatcher.UiCanvasShown.Invoke(canvas);

            if (Keyboard.current != null)
                Keyboard.current.onTextInput += OnPhysicalTextInput;
            Logger.Log("[VirtualKeyboard] Shown", LogCat.Ui);
        }

        private void CreateCharacterRow(string keys, float y)
        {
            var startX = -(keys.Length - 1) * (_keyWidth + _keySpacing) / 2f;
            for (var i = 0; i < keys.Length; i++)
            {
                var character = keys[i];
                var labelChar = char.IsLetter(character) && _isUpperCase ? char.ToUpperInvariant(character) : character;
                var label = CreateKey(new Vector2(startX + i * (_keyWidth + _keySpacing), y),
                    new Vector2(_keyWidth, _keyHeight), labelChar.ToString(), _keyColor, () => Type(character));
                if (char.IsLetter(character))
                    _letterKeys.Add((label, character));
            }
        }

        private void CreateSuggestionRow(float y, float rowWidth)
        {
            var keyWidth = (rowWidth - (_maxSuggestions - 1) * _keySpacing) / _maxSuggestions;
            var startX = -rowWidth / 2f + keyWidth / 2f;
            for (var i = 0; i < _maxSuggestions; i++)
            {
                var index = i;
                var label = CreateKey(new Vector2(startX + i * (keyWidth + _keySpacing), y),
                    new Vector2(keyWidth, _keyHeight), string.Empty, _suggestionColor, () => ApplySuggestion(index));
                label.enableAutoSizing = true;
                label.fontSizeMin = _suggestionMinFontSize;
                label.fontSizeMax = _suggestionFontSize;
                var key = label.transform.parent.gameObject;
                key.SetActive(false);
                _suggestionKeys.Add((key, label));
            }
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

        private TMP_Text CreateKey(Vector2 position, Vector2 size, string label, Color color, Action onClick)
        {
            var text = CreateLabel(transform, position, size);
            text.text = label;
            var keyGo = text.transform.parent.gameObject;
            keyGo.GetComponent<Image>().color = color;
            var button = keyGo.AddComponent<Button>();
            button.targetGraphic = keyGo.GetComponent<Image>();
            // The key under the laser lights up clearly: the tint multiplies the key's color, the multiplier lets the
            // highlight go above it (normal 0.55 x 1.8 = the key's own color, highlighted 1.8x brighter).
            var colors = button.colors;
            colors.normalColor = new Color(0.55f, 0.55f, 0.55f, 1f);
            colors.highlightedColor = Color.white;
            colors.selectedColor = colors.normalColor;
            colors.pressedColor = new Color(0.35f, 0.35f, 0.35f, 1f);
            colors.colorMultiplier = 1.8f;
            colors.fadeDuration = 0.05f;
            button.colors = colors;
            button.onClick.AddListener(() => onClick());
            return text;
        }

        /// <summary>
        /// A rectangle (Image) with a Gothic font text filling it - the text is returned, its parent is the rectangle.
        /// </summary>
        private static TMP_Text CreateLabel(Transform parent, Vector2 position, Vector2 size)
        {
            var boxGo = new GameObject("Key", typeof(RectTransform));
            boxGo.transform.SetParent(parent, false);
            var rect = (RectTransform)boxGo.transform;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            boxGo.AddComponent<Image>();

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(boxGo.transform, false);
            var textRect = (RectTransform)textGo.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            var text = textGo.AddComponent<TextMeshProUGUI>();
            GothicText.Apply(text);
            text.fontSize = _fontSize;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Truncate;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
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
            foreach (var (label, letter) in _letterKeys)
                label.text = (_isUpperCase ? char.ToUpperInvariant(letter) : letter).ToString();
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
                        if (_suggestions.Count >= _maxSuggestions)
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

            for (var i = 0; i < _suggestionKeys.Count; i++)
            {
                var (key, label) = _suggestionKeys[i];
                var isUsed = i < _suggestions.Count;
                key.SetActive(isUsed);
                if (isUsed)
                    label.text = _suggestions[i];
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
            var symbols = _isSymbolRowShown ? _symbolRow : _defaultPhysicalSymbols;
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
