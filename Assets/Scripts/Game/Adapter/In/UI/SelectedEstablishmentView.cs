using TMPro;
using UnityEngine;
using Game.Domain.Service;

namespace Game.Adapter.In.UI
{
    public sealed class SelectedEstablishmentView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI selectedText;
        [SerializeField] private TMP_Text companyText, locationText, segmentText, emptyText;
        [SerializeField] private UnityEngine.UI.Image facade;

        public void Bind(EstablishmentSummary summary)
        {
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
