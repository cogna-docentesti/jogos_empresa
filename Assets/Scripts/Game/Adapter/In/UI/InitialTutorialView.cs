using UnityEngine;

namespace Game.Adapter.In.UI
{
    /// <summary>Hierarchy and editable tutorial texts are saved in the scene.</summary>
    public sealed class InitialTutorialView : MonoBehaviour
    {
        public static bool IsInitial(GameState state) => GameManager.Instance != null
            && GameManager.Instance.StateMachine.CurrentState == state;

        public static void Configure(Transform screen, bool initial)
        {
            var view = screen.GetComponentInChildren<InitialTutorialView>(true);
            if (view != null) view.gameObject.SetActive(initial);
        }

        public void Dismiss() => gameObject.SetActive(false);
    }
}