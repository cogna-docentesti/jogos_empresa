using System.Linq;
using SQLite4Unity3d;

public class BaseRepository<T> where T : new()
{
    protected readonly SQLiteConnection db;

    public BaseRepository(SQLiteConnection connection)
    {
        db = connection;
    }

    public void Insert(T entity) => db.Insert(entity);
    public void Update(T entity) => db.Update(entity);
    public void Delete(T entity) => db.Delete(entity);
    public void InsertOrReplace(T entity) => db.InsertOrReplace(entity);
    public TableQuery<T> Table() => db.Table<T>();
}