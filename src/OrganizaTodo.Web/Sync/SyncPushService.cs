using System.Data;
using System.Globalization;
using Dapper;
using Npgsql;

namespace OrganizaTodo.Web.Sync;

public sealed record SyncRowChange(Guid SyncId, DateTimeOffset UpdatedAt, bool IsDeleted, IReadOnlyDictionary<string, object?> Fields);

public interface ISyncPushService
{
    Task ApplyChangesAsync(int userId, IReadOnlyDictionary<string, IReadOnlyList<SyncRowChange>> changesByTable, CancellationToken ct = default);
}

public sealed class SyncPushService(string connectionString) : ISyncPushService
{
    private static readonly Dictionary<string, SyncTableDescriptor> TablesByJsonKey =
        SyncTableRegistry.Tables.ToDictionary(t => t.JsonKey);

    public async Task ApplyChangesAsync(int userId, IReadOnlyDictionary<string, IReadOnlyList<SyncRowChange>> changesByTable, CancellationToken ct = default)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        foreach (var (jsonKey, rows) in changesByTable)
        {
            var table = TablesByJsonKey[jsonKey];
            var hasDirectUserId = table.Columns.Contains("UserId");

            foreach (var row in rows)
            {
                // Check 1: does the *existing* row (if any) for this SyncId belong to the
                // caller? SyncId is a fully client-supplied UUID, globally unique across all
                // users, not scoped per-user — so `ON CONFLICT ("SyncId")` will happily match
                // and update ANY existing row, regardless of who the payload claims to be.
                // Without this check, a caller could pass their own valid UserId/FK (so the
                // payload-side check below passes) but reuse a SyncId belonging to a
                // different user's existing row, silently corrupting or reassigning it. This
                // must run before the INSERT/UPDATE, and must run even when the payload-side
                // check below is otherwise satisfied.
                if (hasDirectUserId)
                {
                    var existingOwnerUserId = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
                        $"SELECT \"UserId\" FROM \"{table.TableName}\" WHERE \"SyncId\" = @syncId",
                        new { syncId = row.SyncId }, transaction, cancellationToken: ct));

                    if (existingOwnerUserId is not null && existingOwnerUserId != userId)
                    {
                        throw new InvalidOperationException(
                            $"Rejected sync row for table {table.TableName}: existing row for SyncId {row.SyncId} belongs to a different user.");
                    }
                }
                else
                {
                    var existingForeignKeyValue = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
                        $"SELECT \"{table.OwnershipForeignKeyColumn}\" FROM \"{table.TableName}\" WHERE \"SyncId\" = @syncId",
                        new { syncId = row.SyncId }, transaction, cancellationToken: ct));

                    if (existingForeignKeyValue is not null)
                    {
                        var existingRowOwnedByCaller = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
                            $"SELECT EXISTS (SELECT 1 FROM \"{table.OwnershipParentTable}\" WHERE \"Id\" = @foreignKeyValue AND \"UserId\" = @userId)",
                            new { foreignKeyValue = existingForeignKeyValue, userId },
                            transaction, cancellationToken: ct));

                        if (!existingRowOwnedByCaller)
                        {
                            throw new InvalidOperationException(
                                $"Rejected sync row for table {table.TableName}: existing row for SyncId {row.SyncId} does not belong to the authenticated caller.");
                        }
                    }
                }

                // Check 2: does the *incoming payload* claim valid ownership? This catches a
                // caller pushing a brand-new SyncId (no existing row, so Check 1 is a no-op)
                // whose payload directly claims someone else's UserId, or whose FK points at
                // someone else's parent record.
                if (hasDirectUserId)
                {
                    if (!Equals(row.Fields.GetValueOrDefault("UserId"), userId))
                    {
                        throw new InvalidOperationException(
                            $"Rejected sync row for table {table.TableName}: UserId in payload does not match the authenticated caller.");
                    }
                }
                else
                {
                    // Join-based-ownership table (no UserId column of its own) — verify the
                    // referenced parent row actually belongs to the caller via a real query.
                    // Without this check, a client could push a row pointing at another
                    // user's parent record (e.g. someone else's ShoppingListId) and nothing
                    // else in this method would catch it.
                    var foreignKeyValue = row.Fields.GetValueOrDefault(table.OwnershipForeignKeyColumn!);
                    var ownsParent = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
                        $"SELECT EXISTS (SELECT 1 FROM \"{table.OwnershipParentTable}\" WHERE \"Id\" = @foreignKeyValue AND \"UserId\" = @userId)",
                        new { foreignKeyValue, userId },
                        transaction, cancellationToken: ct));

                    if (!ownsParent)
                    {
                        throw new InvalidOperationException(
                            $"Rejected sync row for table {table.TableName}: {table.OwnershipForeignKeyColumn} does not belong to the authenticated caller.");
                    }
                }

                // Check 3: do any *secondary* foreign keys embedded in this row's own business
                // data point at a parent row actually owned by the caller? These are FK columns
                // distinct from the table's own ownership check above (e.g.
                // LedgerTransactions.ParentTransactionId referencing another LedgerTransactions
                // row, or VehicleMaintenanceLogs.VehicleId referencing a Vehicle even though that
                // table already has its own direct UserId check) — a caller can legitimately own
                // the row being pushed (passing Check 2 above) while pointing one of these FKs at
                // a completely different user's row, since Ids are sequential SERIAL integers and
                // trivially enumerable. Without this check, pre-existing app code that joins
                // through these FKs assuming same-user ownership (written before any FK value
                // could be attacker-controlled) would leak the referenced victim row's data back
                // to the attacker.
                foreach (var foreignKey in table.SecondaryForeignKeys)
                {
                    var foreignKeyValue = row.Fields.GetValueOrDefault(foreignKey.Column);
                    if (foreignKeyValue is null)
                    {
                        continue;
                    }

                    var ownsReferencedRow = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
                        $"SELECT EXISTS (SELECT 1 FROM \"{foreignKey.ParentTable}\" WHERE \"Id\" = @foreignKeyValue AND \"UserId\" = @userId)",
                        new { foreignKeyValue, userId }, transaction, cancellationToken: ct));

                    if (!ownsReferencedRow)
                    {
                        throw new InvalidOperationException(
                            $"Rejected sync row for table {table.TableName}: {foreignKey.Column} does not belong to the authenticated caller.");
                    }
                }

                // Only include a column in the generated SQL when the payload actually
                // supplied it (UserId is always included — it's set explicitly from the
                // authenticated caller, never from the untrusted payload value). Columns
                // the client omits (e.g. server-defaulted timestamps like CreatedAt) are
                // left out of both the INSERT and the ON CONFLICT UPDATE entirely, so an
                // insert falls back to the column's DB default instead of an explicit NULL
                // that would violate a NOT NULL constraint, and a conflicting update leaves
                // the existing value untouched rather than clobbering it with NULL.
                var candidateColumns = hasDirectUserId
                    ? table.Columns.Where(c => c != "UserId").Prepend("UserId").ToList()
                    : table.Columns.ToList();
                var columns = candidateColumns.Where(c => c == "UserId" || row.Fields.ContainsKey(c)).ToList();

                // A payload supplying only "UserId" (direct-ownership table — `columns` is
                // never truly empty there, since UserId is always force-included) or zero
                // business columns at all (join-based table) has nothing to write and would
                // otherwise generate a `updateAssignments` clause of empty string, producing a
                // trailing comma before WHERE ("..."IsDeleted" = EXCLUDED."IsDeleted",\nWHERE
                // ...") — invalid SQL Postgres rejects at parse time. Every currently-registered
                // table also has at least one NOT NULL business column with no DEFAULT, so a
                // payload this empty could never legitimately succeed anyway; reject it early
                // with a clear message rather than let it surface as either a SQL syntax error
                // or a confusing NOT NULL constraint violation.
                var businessColumnCount = hasDirectUserId ? columns.Count - 1 : columns.Count;
                if (businessColumnCount == 0)
                {
                    throw new InvalidOperationException(
                        $"Rejected sync row for table {table.TableName}: payload supplies no business columns to write.");
                }

                var insertColumns = string.Join(", ", columns.Select(c => $"\"{c}\""));
                var insertParams = string.Join(", ", columns.Select(c => $"@{c}"));
                var updateAssignments = string.Join(", ", columns.Where(c => c != "UserId").Select(c => $"\"{c}\" = @{c}"));

                var parameters = new DynamicParameters();
                parameters.Add("SyncId", row.SyncId);
                parameters.Add("UpdatedAt", row.UpdatedAt);
                parameters.Add("IsDeleted", row.IsDeleted);
                foreach (var column in columns)
                {
                    var rawValue = column == "UserId" ? (object?)userId : row.Fields.GetValueOrDefault(column);

                    // DateOnly is bound as DbType.Date + a plain DateTime rather than passed
                    // through as a raw System.DateOnly: Dapper's DynamicParameters can't infer a
                    // DbType for DateOnly on its own (LookupDbType throws NotSupportedException)
                    // without the app-wide SqlMapper.AddTypeHandler<DateOnly> registration from
                    // Program.cs being active, which this service shouldn't depend on to
                    // function correctly in isolation.
                    if (rawValue is string dateOnlyText && table.DateOnlyColumns.Contains(column))
                    {
                        parameters.Add(column, ParseDateOnly(dateOnlyText).ToDateTime(TimeOnly.MinValue), DbType.Date);
                    }
                    else if (rawValue is string timestampText && table.TimestampColumns.Contains(column))
                    {
                        parameters.Add(column, ParseTimestamp(timestampText));
                    }
                    else
                    {
                        parameters.Add(column, rawValue);
                    }
                }

                // updateAssignments can never be empty here — the businessColumnCount guard
                // above already rejected the only payload shapes that would leave it empty —
                // so interpolating it directly can no longer produce the trailing-comma syntax
                // error this used to be vulnerable to.
                var sql = $"""
                    INSERT INTO "{table.TableName}" ("SyncId", "UpdatedAt", "IsDeleted", {insertColumns})
                    VALUES (@SyncId, @UpdatedAt, @IsDeleted, {insertParams})
                    ON CONFLICT ("SyncId") DO UPDATE SET
                        "UpdatedAt" = EXCLUDED."UpdatedAt",
                        "IsDeleted" = EXCLUDED."IsDeleted",
                        {updateAssignments}
                    WHERE "{table.TableName}"."UpdatedAt" < EXCLUDED."UpdatedAt"
                    """;

                await connection.ExecuteAsync(new CommandDefinition(sql, parameters, transaction, cancellationToken: ct));
            }
        }

        await transaction.CommitAsync(ct);
    }

    // Normalizes any incoming offset/zone info to its UTC instant, then strips the Kind to
    // Unspecified: every synced TIMESTAMP column here is `timestamp without time zone`, and
    // Npgsql 6+ rejects binding a DateTime with Kind=Utc or Kind=Local to that column type
    // ("Cannot write DateTime with Kind=X to PostgreSQL type 'timestamp without time zone'").
    // Only Kind=Unspecified is accepted, so callers effectively store UTC wall-clock values.
    private static DateTime ParseTimestamp(string value)
    {
        var offset = DateTimeOffset.Parse(
            value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
        return DateTime.SpecifyKind(offset.UtcDateTime, DateTimeKind.Unspecified);
    }

    // DATE columns should normally arrive as a bare "yyyy-MM-dd" string, but fall back to
    // parsing a full timestamp (and taking its date part) in case a client serializes a
    // date-only value as a full ISO-8601 instant instead.
    private static DateOnly ParseDateOnly(string value)
    {
        if (DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateOnly))
        {
            return dateOnly;
        }

        return DateOnly.FromDateTime(ParseTimestamp(value));
    }
}
