using System.Text;
using System.Text.Json;
using MySqlConnector;

var root = Directory.GetCurrentDirectory();
var settingsPath = Path.Combine(root, "appsettings.json");
if (!File.Exists(settingsPath))
{
    throw new FileNotFoundException($"找不到 appsettings.json: {settingsPath}");
}

var outDir = Path.Combine(root, "tools", "datagen", "out");
if (!Directory.Exists(outDir))
{
    throw new DirectoryNotFoundException($"找不到导出目录: {outDir}");
}

var connectionString = ReadConnectionString(settingsPath);
await using var connection = new MySqlConnection(connectionString);
await connection.OpenAsync();

var demoUserId = await GetDemoUserIdAsync(connection);
Console.WriteLine($"Demo user: {demoUserId}");

await ClearDomainAsync(connection);
await ExecuteNonQueryAsync(connection, "SET FOREIGN_KEY_CHECKS=0;");

var totalGenealogies = await ImportGenealogiesAsync(connection, Path.Combine(outDir, "genealogies.csv"), demoUserId);
var totalGenealogyUsers = await ImportGenealogyUsersAsync(connection, Path.Combine(outDir, "genealogy_users.csv"), demoUserId);
var totalPersons = await ImportCsvAsync(
    connection,
    Path.Combine(outDir, "persons.csv"),
    "Persons",
    new[] { "Id", "GenealogyId", "GivenName", "Gender", "BirthYear", "DeathYear", "Bio", "CreatedAt" },
    row => row,
    500);
var totalParentChildren = await ImportCsvAsync(
    connection,
    Path.Combine(outDir, "parent_children.csv"),
    "ParentChildren",
    new[] { "GenealogyId", "ParentId", "ChildId", "RelationshipType" },
    row => row,
    1000);
var totalMarriages = await ImportCsvAsync(
    connection,
    Path.Combine(outDir, "marriages.csv"),
    "Marriages",
    new[] { "GenealogyId", "SpouseAId", "SpouseBId", "MarriedAtYear", "DivorcedAtYear", "Note" },
    row => row,
    1000);

await ExecuteNonQueryAsync(connection, "SET FOREIGN_KEY_CHECKS=1;");

Console.WriteLine("导入完成");
Console.WriteLine($"Genealogies: {totalGenealogies}");
Console.WriteLine($"GenealogyUsers: {totalGenealogyUsers}");
Console.WriteLine($"Persons: {totalPersons}");
Console.WriteLine($"ParentChildren: {totalParentChildren}");
Console.WriteLine($"Marriages: {totalMarriages}");

var countChecks = new[]
{
    ("Genealogies", "SELECT COUNT(*) FROM Genealogies"),
    ("GenealogyUsers", "SELECT COUNT(*) FROM GenealogyUsers"),
    ("Persons", "SELECT COUNT(*) FROM Persons"),
    ("ParentChildren", "SELECT COUNT(*) FROM ParentChildren"),
    ("Marriages", "SELECT COUNT(*) FROM Marriages"),
};
foreach (var (name, sql) in countChecks)
{
    await using var cmd = new MySqlCommand(sql, connection);
    var count = await cmd.ExecuteScalarAsync();
    Console.WriteLine($"{name} rows in DB: {count}");
}

static string ReadConnectionString(string settingsPath)
{
    using var doc = JsonDocument.Parse(File.ReadAllText(settingsPath));
    if (!doc.RootElement.TryGetProperty("ConnectionStrings", out var cs) ||
        !cs.TryGetProperty("DefaultConnection", out var value))
    {
        throw new InvalidOperationException("appsettings.json 中未找到 ConnectionStrings:DefaultConnection");
    }

    return value.GetString() ?? throw new InvalidOperationException("连接字符串为空");
}

static async Task<Guid> GetDemoUserIdAsync(MySqlConnection connection)
{
    const string sql = "SELECT Id FROM Users WHERE Email = @Email LIMIT 1;";
    await using var cmd = new MySqlCommand(sql, connection);
    cmd.Parameters.AddWithValue("@Email", "demo@genealogy.local");
    var result = await cmd.ExecuteScalarAsync();
    if (result is null || result is DBNull)
    {
        throw new InvalidOperationException("未找到 demo@genealogy.local，请先启动一次 Web 程序完成种子数据创建。");
    }

    return Guid.Parse(result.ToString()!);
}

static async Task ClearDomainAsync(MySqlConnection connection)
{
    var statements = new[]
    {
        "DELETE FROM Marriages;",
        "DELETE FROM ParentChildren;",
        "DELETE FROM Persons;",
        "DELETE FROM GenealogyUsers;",
        "DELETE FROM Genealogies;",
    };

    foreach (var statement in statements)
    {
        await ExecuteNonQueryAsync(connection, statement);
    }
}

static async Task ExecuteNonQueryAsync(MySqlConnection connection, string sql)
{
    await using var cmd = new MySqlCommand(sql, connection);
    await cmd.ExecuteNonQueryAsync();
}

static async Task<int> ImportGenealogiesAsync(MySqlConnection connection, string filePath, Guid demoUserId)
{
    return await ImportCsvAsync(
        connection,
        filePath,
        "Genealogies",
        new[] { "Id", "Title", "Surname", "CompiledAt", "CreatedByUserId", "CreatedAt" },
        row =>
        {
            row[4] = demoUserId.ToString();
            return row;
        },
        500);
}

static async Task<int> ImportGenealogyUsersAsync(MySqlConnection connection, string filePath, Guid demoUserId)
{
    return await ImportCsvAsync(
        connection,
        filePath,
        "GenealogyUsers",
        new[] { "GenealogyId", "UserId", "Role", "InvitedByUserId", "InvitedAt" },
        row =>
        {
            row[1] = demoUserId.ToString();
            return row;
        },
        1000);
}

static async Task<int> ImportCsvAsync(
    MySqlConnection connection,
    string filePath,
    string tableName,
    string[] columns,
    Func<string?[], string?[]> rowTransform,
    int batchSize)
{
    var imported = 0;
    var batch = new List<string?[]>(batchSize);

    using var reader = new StreamReader(filePath, Encoding.UTF8);
    var header = await reader.ReadLineAsync();
    if (header is null)
    {
        return 0;
    }

    string? line;
    while ((line = await reader.ReadLineAsync()) is not null)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            continue;
        }

        var row = line.Split(',');
        var transformed = rowTransform(row);
        batch.Add(transformed);
        if (batch.Count >= batchSize)
        {
            await InsertBatchAsync(connection, tableName, columns, batch);
            imported += batch.Count;
            batch.Clear();
        }
    }

    if (batch.Count > 0)
    {
        await InsertBatchAsync(connection, tableName, columns, batch);
        imported += batch.Count;
    }

    return imported;
}

static async Task InsertBatchAsync(MySqlConnection connection, string tableName, string[] columns, List<string?[]> rows)
{
    var sql = new StringBuilder();
    sql.Append("INSERT INTO ").Append(tableName).Append(" (").Append(string.Join(", ", columns)).Append(") VALUES ");

    for (var i = 0; i < rows.Count; i++)
    {
        if (i > 0)
        {
            sql.Append(",");
        }

        sql.Append("(");
        var row = rows[i];
        for (var c = 0; c < columns.Length; c++)
        {
            if (c > 0)
            {
                sql.Append(",");
            }

            sql.Append(ToSqlLiteral(c < row.Length ? row[c] : null));
        }
        sql.Append(")");
    }

    sql.Append(';');
    await using var cmd = new MySqlCommand(sql.ToString(), connection);
    await cmd.ExecuteNonQueryAsync();
}

static string ToSqlLiteral(string? value)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        return "NULL";
    }

    var escaped = value
        .Replace("\\", "\\\\")
        .Replace("'", "''")
        .Replace("\r", "")
        .Replace("\n", "\\n");
    return $"'{escaped}'";
}
