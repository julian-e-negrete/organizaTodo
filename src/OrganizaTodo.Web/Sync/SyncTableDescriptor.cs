namespace OrganizaTodo.Web.Sync;

// A secondary foreign key is a column *within* a synced row's own business data (distinct
// from the table's own OwnershipForeignKeyColumn/OwnershipParentTable, which is how a
// join-based-ownership table's *own* row is owned) that references another row in
// ParentTable. Because the pushed row's own ownership check only verifies the row itself
// (or, for join-based tables, its own parent), a secondary FK value is otherwise
// completely unvalidated — a caller could push a row they legitimately own while pointing
// one of these FKs at another user's row. ParentTable is always a table with its own
// direct "UserId" column, so validating one is a simple ownership EXISTS check.
public sealed record SyncSecondaryForeignKey(string Column, string ParentTable);

public sealed record SyncTableDescriptor(
    string TableName,
    string JsonKey,
    string OwnershipWhereClause,
    IReadOnlyList<string> Columns,
    string? OwnershipForeignKeyColumn = null,
    string? OwnershipParentTable = null)
{
    // FK columns embedded in this table's own business data that reference another row
    // (see SyncSecondaryForeignKey above). Empty for tables with none.
    public IReadOnlyList<SyncSecondaryForeignKey> SecondaryForeignKeys { get; init; } = [];

    // Columns whose Postgres type is TIMESTAMP/TIMESTAMPTZ. JSON has no native date type,
    // so these arrive from the client as plain strings — SyncPushService parses them into
    // a proper DateTime before binding as a Dapper parameter (Npgsql sends a raw C# string
    // as SQL `text`, which Postgres rejects for a timestamp column: 42804).
    public IReadOnlyList<string> TimestampColumns { get; init; } = [];

    // Columns whose Postgres type is DATE (as opposed to TIMESTAMP/TIMESTAMPTZ above) —
    // parsed into a DateOnly instead of a DateTime so Npgsql binds them as SQL `date`.
    public IReadOnlyList<string> DateOnlyColumns { get; init; } = [];
}
