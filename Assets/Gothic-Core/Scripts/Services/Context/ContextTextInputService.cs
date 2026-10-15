using System;

namespace Gothic.Core.Services.Context
{
    public class ContextTextInputService : IContextTextInputService
    {
        private IContextTextInputService _impl;

        public void SetImpl(IContextTextInputService impl)
        {
            _impl = impl;
        }

        public void RequestText(string initialText, int maxLength, Action<string> onSubmit, Action onCancel,
            Action<string> onTextChanged = null)
        {
            _impl.RequestText(initialText, maxLength, onSubmit, onCancel, onTextChanged);
        }
    }
}
