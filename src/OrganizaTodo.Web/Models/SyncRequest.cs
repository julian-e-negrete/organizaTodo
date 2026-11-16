namespace OrganizaTodo.Web.Models;

public sealed record SyncRowChangeDto(Guid SyncId, DateTimeOffset UpdatedAt, bool IsDeleted, Dictionary<string, object?> Fields);

public sealed record SyncRequest(DateTimeOffset Since, Dictionary<string, List<SyncRowChangeDto>> Changes);
