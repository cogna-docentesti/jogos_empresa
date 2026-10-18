using TMPro;
using UnityEngine;

namespace Game.Adapter.In.UI.Theme
{
    /// <summary>Copies with the text template. Re-entry restores its face without changing content or dimensions.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(TMP_Text))]
    public sealed class TypographyRoleBinding : MonoBehaviour
    {
        [SerializeField] private TypographyRole role;
        public TypographyRole Role => role;
        public void SetRole(TypographyRole value) { role = value; }
        private void OnEnable() => Reapply();
        public void Reapply() => GameTypography.ApplyFace(GetComponent<TMP_Text>(), role);
    }
}
