using System.Security.Claims;

namespace AILearningPlatform.ApiServer.Auth;

/// <summary>Hilfsmethoden für den angemeldeten Benutzer (aus dem JWT).</summary>
public static class CurrentUser
{
    public static Guid? GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue("sub") ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var id) ? id : null;
    }

    /// <summary>
    /// Liefert 403, wenn die angefragte Benutzer-ID nicht die des angemeldeten Benutzers ist,
    /// sonst null. Verhindert, dass jemand fremde Daten über eine andere ID abruft.
    /// </summary>
    public static IResult? ForbidUnlessSelf(this ClaimsPrincipal principal, Guid requestedUserId) =>
        principal.GetUserId() == requestedUserId ? null : Results.Forbid();
}
