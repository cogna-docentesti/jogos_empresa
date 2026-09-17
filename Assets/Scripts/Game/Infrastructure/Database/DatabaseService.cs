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
        Connection = new SQLiteConnection(dbPath);
    }

    public void Close()
    {
        Connection?.Close();
        Connection = null;
    }
}
