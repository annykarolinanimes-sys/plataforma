using System.Security.Claims;

namespace Accusoft.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static int GetUserId(this ClaimsPrincipal principal)
    {
        var claim = principal.FindFirst("userId") ?? principal.FindFirst(ClaimTypes.NameIdentifier);
        if (claim == null || !int.TryParse(claim.Value, out var id))
            throw new UnauthorizedAccessException("Token inválido: userId em falta.");
        return id;
    }

    public static bool IsAdmin(this ClaimsPrincipal principal) =>
        principal.IsInRole("admin");
}