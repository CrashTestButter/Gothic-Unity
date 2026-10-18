using System;

namespace Gothic.Core.Services.Context
{
    public interface IContextTextInputService
    {
        /// <summary>
        /// Lets the player type a text (e.g. a save game name) - VR: on-screen keyboard, Flat: physical keyboard.
        /// </summary>
        /// <param name="onSubmit">Called with the trimmed text.</param>
        /// <param name="onCancel">Called when the player cancels. Can be null.</param>
        /// <param name="onTextChanged">Called with the text after every change, to show it. Can be null.</param>
        void RequestText(string initialText, int maxLength, Action<string> onSubmit, Action onCancel,
            Action<string> onTextChanged = null);
    }
}
