using MySqlConnector;

namespace CleanHome.ServiceDefaults;

// Hạ tầng ADO.NET dùng chung; câu SQL và quyền sở hữu dữ liệu nằm tại từng service.
public static class DatabaseQueries
{
    public static MySqlCommand Sql(this MySqlConnection connection, string text, params (string Name, object? Value)[] values)
    {
        var command = new MySqlCommand(text, connection) { CommandTimeout = 10 };
        foreach (var (name, value) in values) command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        return command;
    }

    public static async Task<List<Dictionary<string, object?>>> RowsAsync(this MySqlCommand command, CancellationToken ct)
    {
        var rows = new List<Dictionary<string, object?>>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var row = new Dictionary<string, object?>();
            for (var i = 0; i < reader.FieldCount; i++)
                row[reader.GetName(i)] = await reader.IsDBNullAsync(i, ct) ? null : reader.GetValue(i);
            rows.Add(row);
        }
        return rows;
    }
}
