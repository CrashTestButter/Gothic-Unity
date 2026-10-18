using System;
using Gothic.Core.Logging;
using UnityEngine;
using UnityEngine.InputSystem;
using Logger = Gothic.Core.Logging.Logger;

namespace Gothic.Flat.Adapters.UI
{
    /// <summary>
    /// Flat mode text input without own UI: types with the physical keyboard, the caller shows the text
    /// (onTextChanged). Enter submits, Escape cancels, Backspace deletes.
    /// </summary>
    public class FlatTextInput : MonoBehaviour
    {
        private static FlatTextInput _current;

        private string _text;
        private int _maxLength;
        private int _shownFrame;
        private Action<string> _onSubmit;
        private Action _onCancel;
        private Action<string> _onTextChanged;

        public static void Show(string initialText, int maxLength, Action<string> onSubmit, Action onCancel,
            Action<string> onTextChanged)
        {
            if (_current != null)
                Destroy(_current.gameObject);

            _current = new GameObject(nameof(FlatTextInput)).AddComponent<FlatTextInput>();
            _current._maxLength = Mathf.Max(1, maxLength);
            _current._shownFrame = Time.frameCount;
            _current._text = initialText ?? string.Empty;
            _current._onSubmit = onSubmit;
            _current._onCancel = onCancel;
            _current._onTextChanged = onTextChanged;
        }

        private void OnEnable()
        {
            if (Keyboard.current != null)
                Keyboard.current.onTextInput += OnTextInput;
        }

        private void OnDisable()
        {
            if (Keyboard.current != null)
                Keyboard.current.onTextInput -= OnTextInput;
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            // The Enter which opened us (menu navigation) mustn't submit right away.
            if (keyboard == null || Time.frameCount == _shownFrame)
                return;

            if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
            {
                var text = _text.Trim();
                Logger.Log($"[FlatTextInput] Submitted '{text}'", LogCat.Ui);
                var onSubmit = _onSubmit;
                Destroy(gameObject);
                onSubmit?.Invoke(text);
            }
            else if (keyboard.escapeKey.wasPressedThisFrame)
            {
                Logger.Log("[FlatTextInput] Cancelled", LogCat.Ui);
                var onCancel = _onCancel;
                Destroy(gameObject);
                onCancel?.Invoke();
            }
            else if (keyboard.backspaceKey.wasPressedThisFrame && _text.Length > 0)
            {
                _text = _text.Substring(0, _text.Length - 1);
                _onTextChanged?.Invoke(_text);
            }
        }

        private void OnTextInput(char character)
        {
            // Control characters (Enter, Backspace, ...) are handled in Update().
            if (char.IsControl(character) || _text.Length >= _maxLength)
                return;

            _text += character;
            _onTextChanged?.Invoke(_text);
        }

        private void OnDestroy()
        {
            if (_current == this)
                _current = null;
        }
    }
}
