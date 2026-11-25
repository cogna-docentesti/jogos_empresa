using Game.Adapter.In.Controllers;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    internal static class MonthlyCalendarUiMigration
    {
        internal static void Wire(Transform canvas, MonthlySettlementController settlement)
        {
            var root = canvas.Find("Panel_MonthlyCalendar") as RectTransform;
            if (root == null)
            {
                root = (RectTransform)new GameObject("Panel_MonthlyCalendar", typeof(RectTransform)).transform;
                root.SetParent(canvas, false);
                root.anchorMin = Vector2.zero;
                root.anchorMax = Vector2.one;
                root.offsetMin = root.offsetMax = Vector2.zero;
            }
            root.gameObject.SetActive(false);
            var controller = root.GetComponent<MonthlyCalendarController>() ?? root.gameObject.AddComponent<MonthlyCalendarController>();
            Set(settlement, "calendar", controller);
            Set(canvas.GetComponentInParent<UIStateListener>(true) ?? Object.FindFirstObjectByType<UIStateListener>(),
                "monthlyCalendarPanel", root.gameObject);
            var eventPanel = canvas.Find("Panel_Event");
            root.SetSiblingIndex(eventPanel.GetSiblingIndex());
        }

        private static void Set(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
