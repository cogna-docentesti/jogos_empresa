using UnityEngine;

public class GameManager : MonoBehaviour
{
    private void Start()
    {
        GameSessionState.LoadActiveSession("local_user_01");
    }
}
