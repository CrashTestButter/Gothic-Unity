using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gothic.VR.Models.UI
{
    /// <summary>
    /// Settings of one VirtualKeyboard session. Everything is optional except the callbacks you care about.
    /// </summary>
    public class VirtualKeyboardOptions
    {
        [Tooltip("Text already typed when the keyboard opens.")]
        public string InitialText = string.Empty;

        [Tooltip("Called with the trimmed text on OK/Enter.")]
        public Action<string> OnSubmit;

        [Tooltip("Called on X/Escape.")]
        public Action OnCancel;

        [Tooltip("Maximum characters (30 for save names, ~64 for cheat commands).")]
        public int MaxLength = 30;

        [Tooltip("Every letter is typed in upper case (Gothic symbols), shift does nothing.")]
        public bool IsForcedUpperCase;

        [Tooltip("Adds a row with _ - . , : +")]
        public bool IsSymbolRowShown;

        [Tooltip("Gets the current text after each change and returns suggestions - the first ones are shown as "
                 + "buttons between the text and the keys; tapping one replaces the text.")]
        public Func<string, IReadOnlyList<string>> SuggestionProvider;

        [Tooltip("Called with the current text and a key's character (after case handling). false greys the key out. "
                 + "null allows everything.")]
        public Func<string, char, bool> KeyFilter;

        [Tooltip("Called with the current text. false disables OK. null allows always.")]
        public Func<string, bool> CanSubmit;

        [Tooltip("Called whenever the user changes the text (typing, backspace, suggestion), not for SetText().")]
        public Action<string> OnTextChanged;

        [Tooltip("false keeps the keyboard open after OK (onSubmit is still called).")]
        public bool IsClosedOnSubmit = true;

        [Tooltip("If set, the keyboard copies position, rotation and world scale of this anchor every frame instead of standing "
                 + "in front of the camera. It is not parented. Closes when the anchor is destroyed.")]
        public Transform FollowTarget;
    }
}
