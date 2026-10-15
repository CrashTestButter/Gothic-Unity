using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gothic.Core.Adapters.UI
{
    /// <summary>
    /// A character key of the VirtualKeyboard prefab. The keyboard wires the button and updates the label (shift).
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class VirtualKeyboardKey : MonoBehaviour
    {
        [Tooltip("Character typed by this key (first char used). Letters follow shift / forced upper case.")]
        [SerializeField] private string _character;

        [SerializeField] private TMP_Text _label;

        public char Character => string.IsNullOrEmpty(_character) ? ' ' : _character[0];
        public TMP_Text Label => _label;
        public Button Button => GetComponent<Button>();
    }
}
