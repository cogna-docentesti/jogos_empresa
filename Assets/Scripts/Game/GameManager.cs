using UnityEngine;

public class GameManager : MonoBehaviour
{
    private void Start()
    {
        GameSessionState.LoadActiveSession("local_user_01");
    }
    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
            GameSessionState.Save();
    }

    private void OnApplicationQuit()
    {
        GameSessionState.Save();
    }
}
