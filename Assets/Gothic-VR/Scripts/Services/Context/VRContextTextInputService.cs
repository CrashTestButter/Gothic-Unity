using System;
using Gothic.Core.Services.Context;
using Gothic.VR.Adapters.UI;
using Gothic.VR.Models.UI;

namespace Gothic.VR.Services.Context
{
    public class VRContextTextInputService : IContextTextInputService
    {
        public void RequestText(string initialText, int maxLength, Action<string> onSubmit, Action onCancel,
            Action<string> onTextChanged = null)
        {
            VirtualKeyboard.Show(new VirtualKeyboardOptions
            {
                InitialText = initialText,
                MaxLength = maxLength,
                OnSubmit = onSubmit,
                OnCancel = onCancel,
                OnTextChanged = onTextChanged
            });
        }
    }
}
