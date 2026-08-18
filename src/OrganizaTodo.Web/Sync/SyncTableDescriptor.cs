namespace OrganizaTodo.Web.Sync;

public sealed record SyncTableDescriptor(
    string TableName,
    string JsonKey,
    string OwnershipWhereClause,
    IReadOnlyList<string> Columns,
    string? OwnershipForeignKeyColumn = null,
    string? OwnershipParentTable = null);
