using Unity.VisualScripting;
using UnityEngine;

public class DatabaseInitializer : MonoBehaviour
{
    public static DatabaseService DatabaseService { get; private set; }

    /// <summary>
    /// Verdadeiro quando o banco abriu e as tabelas existem. Quando for falso,
    /// o jogo roda mesmo assim - so nao persiste nada.
    /// </summary>
    public static bool IsReady { get; private set; }

    /// <summary>Motivo da falha, para a UI poder avisar o jogador.</summary>
    public static string FailureReason { get; private set; }

    private void Awake()
    {
        if (DatabaseService != null)
        {
            Destroy(gameObject);
            return;
        }

        DontDestroyOnLoad(gameObject);

        DatabaseService = GetComponent<DatabaseService>();

        if (DatabaseService == null)
        {
            Fail("O GameObject do DatabaseInitializer nao tem um DatabaseService junto.");
            return;
        }

        // Antes isto rodava solto. Qualquer excecao aqui subia pelo Awake,
        // derrubava o GameManager.Start() junto e o jogo ficava parado na
        // Bootstrap, numa tela vazia, sem uma unica mensagem para o jogador.
        // Foi exatamente o que aconteceu no Android quando faltou o
        // libsqlite3.so da arquitetura em uso.
        try
        {
            DatabaseService.Initialize();

            var db = DatabaseService.Connection;

            db.CreateTable<GameSessionEntity>();
            db.CreateTable<RoundResultEntity>();

            IsReady = true;
        }
        catch (System.DllNotFoundException e)
        {
            Fail("A biblioteca nativa 'sqlite3' nao existe para a arquitetura deste "
               + "aparelho. Confira se ha um libsqlite3.so para a ABI em uso em "
               + "Assets/Plugins/SQLite4Unity3d/Plugins/Android/libs. Detalhe: " + e.Message);
        }
        catch (System.Exception e)
        {
            Fail("Nao foi possivel abrir o banco em " + Application.persistentDataPath
               + ". Detalhe: " + e.Message);
        }
    }

    private static void Fail(string reason)
    {
        IsReady = false;
        FailureReason = reason;

        Debug.LogError("[DatabaseInitializer] " + reason
                     + "\nO jogo continua, mas SEM persistencia: a partida roda e nada e salvo.");
    }
}
