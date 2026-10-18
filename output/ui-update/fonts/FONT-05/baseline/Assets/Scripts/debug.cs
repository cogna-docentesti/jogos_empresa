using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class RaycastDebugger : MonoBehaviour
{
    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            var results = new List<RaycastResult>();
            var data = new PointerEventData(EventSystem.current)
            {
                position = Input.mousePosition
            };

            EventSystem.current.RaycastAll(data, results);

            Debug.Log($"[Raycast] {results.Count} objetos atingidos:");
            foreach (var r in results)
                Debug.Log($"  → {r.gameObject.name} (depth {r.depth})");
        }
    }
}