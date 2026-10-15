using System;
using Gothic.Core.Services.Context;
using Gothic.Flat.Adapters.UI;

namespace Gothic.Flat.Services.Context
{
    public class FlatContextTextInputService : IContextTextInputService
    {
        public void RequestText(string initialText, int maxLength, Action<string> onSubmit, Action onCancel,
            Action<string> onTextChanged = null)
        {
            FlatTextInput.Show(initialText, maxLength, onSubmit, onCancel, onTextChanged);
        }
    }
}
