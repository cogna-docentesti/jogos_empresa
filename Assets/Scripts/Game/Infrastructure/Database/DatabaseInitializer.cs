using Unity.VisualScripting;
using UnityEngine;

public class DatabaseInitializer : MonoBehaviour
{
    public static DatabaseService DatabaseService { get; private set; }

    private void Awake()
    {
        if (DatabaseService != null)
        {
            Destroy(gameObject);
            return;
        }

        DontDestroyOnLoad(gameObject);

        DatabaseService = GetComponent<DatabaseService>();
        DatabaseService.Initialize();

        var db = DatabaseService.Connection;

        db.CreateTable<GameSessionEntity>();
        db.CreateTable<RoundResultEntity>();
       
    }
/*
    private void OnApplicationQuit()
    {
        DatabaseService?.Close();
    }
*/
}
