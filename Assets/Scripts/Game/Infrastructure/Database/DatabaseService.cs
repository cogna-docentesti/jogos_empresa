using UnityEngine;
using SQLite4Unity3d;
using System.IO;
using System.Linq;

public class DatabaseService : MonoBehaviour
{
    public SQLiteConnection Connection { get; private set; }

    public void Initialize()
    {
        string dbPath = Path.Combine(Application.persistentDataPath, "game.db");
#if UNITY_EDITOR || DEVELOPMENT_BUILD || FONT10_QA_PLAYER
        // Explicit QA builds can use disposable storage; ordinary release builds keep the original path.
        string testPath = System.Environment.GetEnvironmentVariable("COGNA_TEST_DATABASE_PATH");
        if (!string.IsNullOrEmpty(testPath)) dbPath = testPath;
#endif
        Connection = new SQLiteConnection(dbPath);
    }

    public void Close()
    {
        Connection?.Close();
        Connection = null;
    }
}
