namespace OrganizaTodo.Web.Models;

public sealed record SyncResponse(DateTimeOffset SyncedAt, Dictionary<string, IReadOnlyList<dynamic>> Changes);
