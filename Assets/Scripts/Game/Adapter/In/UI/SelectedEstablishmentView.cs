using TMPro;
using UnityEngine;
using Game.Domain.Service;
using Game.Adapter.In.UI.Theme;

namespace Game.Adapter.In.UI
{
    public sealed class SelectedEstablishmentView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI selectedText;
        [SerializeField] private TMP_Text companyText, locationText, segmentText, emptyText;
        [SerializeField] private UnityEngine.UI.Image facade;

        public void Bind(EstablishmentSummary summary)
        {
            GameTypography.KeepSize(companyText, TypographyRole.SectionTitle);
            foreach (var t in new TMP_Text[] { selectedText, locationText, emptyText }) GameTypography.KeepSize(t, TypographyRole.Body);
            GameTypography.KeepSize(segmentText, TypographyRole.Label);
            SetSelectedText(segmentText != null ? summary.RestaurantLabel : summary.RestaurantLabel + " · Público " + summary.SegmentLabel.ToLowerInvariant());
            if (companyText != null) companyText.text = summary.CompanyLabel;
            if (locationText != null) locationText.text = summary.LocationLabel;
            if (segmentText != null) segmentText.text = "Público · " + summary.SegmentLabel;
            if (facade != null) { facade.sprite = summary.Facade; facade.enabled = summary.Facade != null; }
            if (emptyText != null) emptyText.gameObject.SetActive(summary.Facade == null);
        }

        public void SetSelectedText(string text)
        {
            if (selectedText != null) selectedText.text = text;
        }
    }
}
