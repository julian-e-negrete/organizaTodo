using System.Security.Claims;

namespace OrganizaTodo.Web.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static int GetUserId(this ClaimsPrincipal principal)
        => int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public static string GetUserRole(this ClaimsPrincipal principal)
        => principal.FindFirstValue(ClaimTypes.Role) ?? "USER";

    public static string GetFullName(this ClaimsPrincipal principal)
        => principal.FindFirstValue(ClaimTypes.Name) ?? string.Empty;
}
