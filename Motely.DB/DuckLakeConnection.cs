using DuckDB.NET.Data;

namespace Motely.DB;

public sealed class DuckLakeConnection : IDisposable
{
    private readonly DuckDBConnection _conn;

    public DuckLakeConnection(string dbPath, string lakeName = "motely_lake")
    {
        _conn = new DuckDBConnection("Data Source=:memory:");
        _conn.Open();

        if (dbPath != ":memory:")
        {
            var (lakeDir, metaFile, dataDir) = ResolveLakePaths(dbPath);
            Directory.CreateDirectory(lakeDir);
            Directory.CreateDirectory(dataDir);

            Execute("INSTALL ducklake; LOAD ducklake;");
            Execute($"ATTACH 'ducklake:{EscapePath(metaFile)}' AS {lakeName} (DATA_PATH '{EscapePath(dataDir)}');");
            Execute($"USE {lakeName};");
        }
    }

    public DuckDBCommand CreateCommand() => _conn.CreateCommand();

    public DuckDBAppender CreateAppender(string table) => _conn.CreateAppender(table);

    public void Execute(string sql)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    public void Dispose() => _conn.Dispose();

    internal static (string LakeDir, string MetaFile, string DataDir) ResolveLakePaths(string dbPath)
    {
        var fullPath = Path.GetFullPath(dbPath);

        if (!Path.HasExtension(fullPath))
            return (fullPath, Path.Combine(fullPath, "metadata.ducklake"), Path.Combine(fullPath, "data"));

        var directory = Path.GetDirectoryName(fullPath);
        var baseName = Path.GetFileNameWithoutExtension(fullPath);
        var basePath = string.IsNullOrWhiteSpace(directory)
            ? Path.Combine(Directory.GetCurrentDirectory(), baseName)
            : Path.Combine(directory, baseName);
        var lakeDir = $"{basePath}_lake";
        return (lakeDir, Path.Combine(lakeDir, "metadata.ducklake"), Path.Combine(lakeDir, "data"));
    }

    internal static string EscapePath(string path) => path.Replace("\\", "/").Replace("'", "''");

    internal static string EscapeLiteral(string value) => value.Replace("'", "''");
}
