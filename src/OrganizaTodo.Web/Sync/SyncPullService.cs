using Dapper;
using Npgsql;

namespace OrganizaTodo.Web.Sync;

public interface ISyncPullService
{
    Task<Dictionary<string, IReadOnlyList<dynamic>>> PullChangesAsync(int userId, DateTimeOffset since, CancellationToken ct = default);
}

public sealed class SyncPullService(string connectionString) : ISyncPullService
{
    public async Task<Dictionary<string, IReadOnlyList<dynamic>>> PullChangesAsync(int userId, DateTimeOffset since, CancellationToken ct = default)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);

        var result = new Dictionary<string, IReadOnlyList<dynamic>>();

        foreach (var table in SyncTableRegistry.Tables)
        {
            var sql = $"""
                SELECT "Id", "SyncId", "UpdatedAt", "IsDeleted", {string.Join(", ", table.Columns.Select(c => $"\"{c}\""))}
                FROM "{table.TableName}"
                WHERE {table.OwnershipWhereClause} AND "UpdatedAt" > @since
                """;

            var rows = await connection.QueryAsync(new CommandDefinition(sql, new { userId, since }, cancellationToken: ct));
            result[table.JsonKey] = rows.ToList();
        }

        return result;
    }
}
