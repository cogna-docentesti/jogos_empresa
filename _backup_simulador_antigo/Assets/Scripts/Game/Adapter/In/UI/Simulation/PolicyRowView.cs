using System;
using System.Collections.Generic;
using System.Linq;
using Game.Adapter.In.UI.Theme;
using Game.Domain.Simulation;
using TMPro;
using UnityEngine;

namespace Game.Adapter.In.UI.Simulation
{
    /// <summary>Uma politica de gestao no planejamento do mes (prefab PolicyRow): nome, opcoes e efeito.</summary>
    public sealed class PolicyRowView : MonoBehaviour
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Dropdown dropdown;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private TMP_Text costText;

        private PolicyCategory _category;
        private Action<string, string> _onChanged;

        public string CategoryId => _category?.id;

        public string SelectedOptionId =>
            _category == null || dropdown == null || dropdown.value < 0 || dropdown.value >= _category.options.Count
                ? null
                : _category.options[dropdown.value].id;

        public void Bind(PolicyCategory category, string selectedOptionId, Action<string, string> onChanged)
        {
            _category = category;
            _onChanged = onChanged;
            if (titleText != null) titleText.text = category.displayName;
            if (dropdown != null)
            {
                dropdown.onValueChanged.RemoveAllListeners();
                dropdown.ClearOptions();
                dropdown.AddOptions(category.options.Select(o => o.displayName).ToList());
                int index = Math.Max(0, category.options.FindIndex(o => o.id == selectedOptionId));
                dropdown.SetValueWithoutNotify(index);
                dropdown.onValueChanged.AddListener(_ => { RenderSelected(); _onChanged?.Invoke(_category.id, SelectedOptionId); });
            }
            RenderSelected();
        }

        private void RenderSelected()
        {
            var option = _category?.Option(SelectedOptionId);
            if (option == null) return;
            if (descriptionText != null) descriptionText.text = option.description ?? _category.description;
            if (costText != null)
            {
                costText.text = option.monthlyCost > 0.004d ? SimFormat.Money(-option.monthlyCost, false) + "/mês" : "Sem custo fixo";
                costText.color = option.monthlyCost > 0.004d ? GamePalette.Warn : GamePalette.Muted;
            }
        }
    }
}
