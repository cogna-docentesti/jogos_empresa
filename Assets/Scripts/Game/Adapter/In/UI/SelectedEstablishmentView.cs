using TMPro;
using UnityEngine;

namespace Game.Adapter.In.UI
{
    public sealed class SelectedEstablishmentView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI selectedText;

        public void SetSelectedText(string text)
        {
            selectedText.text = text;
        }
    }
}