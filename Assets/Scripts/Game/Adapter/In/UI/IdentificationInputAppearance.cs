using TMPro;
using UnityEngine;

namespace Game.Adapter.In.UI
{
    [RequireComponent(typeof(TMP_InputField))]
    public sealed class IdentificationInputAppearance : MonoBehaviour
    {
        [SerializeField] private Sprite normal;
        [SerializeField] private Sprite focus;
        [SerializeField] private Sprite invalid;
        [SerializeField] private Sprite disabled;
        private TMP_InputField input;
        public bool IsInvalid { get; set; }

        private void Awake() => input = GetComponent<TMP_InputField>();

        private void LateUpdate()
        {
            if (input == null || input.image == null) return;
            input.image.sprite = !input.interactable ? disabled
                : IsInvalid ? invalid : input.isFocused ? focus : normal;
        }
    }
}
