using System;
using Gothic.Core.Logging;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Logger = Gothic.Core.Logging.Logger;

namespace Gothic.Core.Adapters.UI
{
    /// <summary>
    /// DeveloperConfig.EnableSaveNameKeyboard: an on-screen keyboard in Gothic's font (like SteamVR's) - a panel with the
    /// typed text, letters, digits, shift, space, backspace, OK and cancel, clicked with the UI laser. It shows in front
    /// of the camera; a physical keyboard types into it as well (flat mode, Editor).
    /// </summary>
    public class VirtualKeyboard : MonoBehaviour
    {
        private const int _maxLength = 30;
        private const float _distance = 1.2f;
        private const float _heightBelowEyes = 0.25f;
        private const float _pixelSize = 0.001f;
        private const float _keyWidth = 70f;
        private const float _keyHeight = 60f;
        private const float _keySpacing = 8f;
        private const float _fontSize = 34f;

        private static readonly string[] _rows = { "1234567890", "qwertyuiop", "asdfghjkl", "zxcvbnm" };
        private static readonly Color _panelColor = new(0.05f, 0.04f, 0.03f, 0.92f);
        private static readonly Color _keyColor = new(0.25f, 0.21f, 0.16f, 0.95f);
        private static readonly Color _actionKeyColor = new(0.35f, 0.27f, 0.15f, 0.95f);

        private static VirtualKeyboard _current;

        private TMP_Text _display;
        private string _text = string.Empty;
        private bool _isUpperCase;
        private Action<string> _onSubmit;
        private Action _onCancel;
        private readonly System.Collections.Generic.List<(TMP_Text Label, char Letter)> _letterKeys = new();

        public static void Show(string initialText, Action<string> onSubmit, Action onCancel)
        {
            if (_current != null)
                _current.Close();

            var go = new GameObject("VirtualKeyboard", typeof(RectTransform));
            _current = go.AddComponent<VirtualKeyboard>();
            _current.Build(initialText ?? string.Empty, onSubmit, onCancel);
        }

        private void Build(string initialText, Action<string> onSubmit, Action onCancel)
        {
            _onSubmit = onSubmit;
            _onCancel = onCancel;
            _text = initialText.Length > _maxLength ? initialText.Substring(0, _maxLength) : initialText;

            PlaceInFrontOfCamera();

            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 100;
            gameObject.AddComponent<GraphicRaycaster>();
            var width = 10 * (_keyWidth + _keySpacing) + 3 * _keySpacing;
            var height = 6 * (_keyHeight + _keySpacing) + 3 * _keySpacing;
            ((RectTransform)transform).sizeDelta = new Vector2(width, height);
            transform.localScale = Vector3.one * _pixelSize;

            var panel = gameObject.AddComponent<Image>();
            panel.color = _panelColor;

            // The typed text on top, then the rows - each row centered.
            var top = height / 2f - _keySpacing * 2f - _keyHeight / 2f;
            _display = CreateLabel(transform, new Vector2(0f, top), new Vector2(width - 4f * _keySpacing, _keyHeight));
            var displayBackground = _display.transform.parent.GetComponent<Image>();
            displayBackground.color = new Color(0f, 0f, 0f, 0.6f);

            for (var row = 0; row < _rows.Length; row++)
            {
                var keys = _rows[row];
                var y = top - (row + 1) * (_keyHeight + _keySpacing);
                var startX = -(keys.Length - 1) * (_keyWidth + _keySpacing) / 2f;
                for (var i = 0; i < keys.Length; i++)
                {
                    var letter = keys[i];
                    var label = CreateKey(new Vector2(startX + i * (_keyWidth + _keySpacing), y),
                        new Vector2(_keyWidth, _keyHeight), letter.ToString(), _keyColor, () => Type(letter));
                    if (char.IsLetter(letter))
                        _letterKeys.Add((label, letter));
                }
            }

            // Bottom row: shift, space, backspace, OK, cancel.
            var bottom = top - 5 * (_keyHeight + _keySpacing);
            var unit = _keyWidth + _keySpacing;
            CreateKey(new Vector2(-4f * unit, bottom), new Vector2(unit * 1.5f - _keySpacing, _keyHeight), "Aa",
                _actionKeyColor, ToggleCase);
            CreateKey(new Vector2(-1.25f * unit, bottom), new Vector2(unit * 3.5f - _keySpacing, _keyHeight), "_",
                _keyColor, () => Type(' '));
            CreateKey(new Vector2(1.5f * unit, bottom), new Vector2(unit * 1.5f - _keySpacing, _keyHeight), "<-",
                _actionKeyColor, Backspace);
            CreateKey(new Vector2(3f * unit, bottom), new Vector2(unit * 1.5f - _keySpacing, _keyHeight), "OK",
                new Color(0.2f, 0.45f, 0.2f, 0.95f), Submit);
            CreateKey(new Vector2(4.25f * unit, bottom), new Vector2(unit - _keySpacing, _keyHeight), "X",
                new Color(0.5f, 0.2f, 0.2f, 0.95f), Cancel);

            RefreshDisplay();
            GlobalEventDispatcher.UiCanvasShown.Invoke(canvas);

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

        private void RefreshDisplay()
        {
            _display.text = _text + "_";
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
            if (char.IsLetterOrDigit(character) || character == ' ' || character == '-' || character == '_')
            {
                if (_text.Length < _maxLength)
                {
                    _text += character;
                    RefreshDisplay();
                }
            }
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
