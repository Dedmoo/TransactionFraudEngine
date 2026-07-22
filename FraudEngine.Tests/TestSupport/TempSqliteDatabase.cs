using FraudEngine.Api.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FraudEngine.Tests.TestSupport;

/// <summary>
/// Creates a unique, file-backed SQLite database for a single test and deletes it on dispose.
/// Used instead of an in-memory provider so tests exercise the same durable storage as production.
/// </summary>
internal sealed class TempSqliteDatabase : IDisposable
{
    private readonly string _path;

    public TempSqliteDatabase()
    {
        _path = Path.Combine(Path.GetTempPath(), $"fraud-engine-{Guid.NewGuid():N}.db");
    }

    public string ConnectionString => $"Data Source={_path}";

    /// <summary>Opens a fresh <see cref="FraudDbContext"/> instance against the same file, applying pending migrations.</summary>
    public FraudDbContext CreateContext()
    {
        var context = new FraudDbContext(new DbContextOptionsBuilder<FraudDbContext>().UseSqlite(ConnectionString).Options);
        context.Database.Migrate();
        return context;
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_path))
            File.Delete(_path);
    }
}
