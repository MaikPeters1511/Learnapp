using System.Net.Mail;
using System.Security.Claims;
using AILearningPlatform.Data;
using AILearningPlatform.Data.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace AILearningPlatform.ApiServer.Auth;

public record RegisterRequest(string? Name, string? Email, string? Password);
public record LoginRequest(string? Email, string? Password);
public record AuthUser(Guid Id, string Name, string Email);
public record AuthResponse(string Token, DateTime ExpiresAt, AuthUser User);

public static class AuthEndpoints
{
    public const int MinPasswordLength = 8;
    public const int MaxPasswordLength = 128;
    private const string InvalidCredentials = "E-Mail oder Passwort ist falsch.";
    private const string EmailTaken = "Diese E-Mail-Adresse wird bereits verwendet.";

    // Wird für Anmeldeversuche mit unbekannter E-Mail geprüft, damit die Antwortzeit nicht verrät,
    // ob es ein Konto gibt.
    private static readonly User DummyUser = new();
    private static readonly Lazy<string> DummyHash =
        new(() => new PasswordHasher<User>().HashPassword(DummyUser, Guid.NewGuid().ToString("N")));

    /// <summary>Registrierung und Anmeldung (öffentlich, mit Rate-Limit).</summary>
    public static void MapPublicAuthEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/auth").RequireRateLimiting("auth");
        group.MapPost("/register", Register);
        group.MapPost("/login", Login);
    }

    /// <summary>Endpunkte für angemeldete Benutzer.</summary>
    public static void MapAuthenticatedAuthEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/auth/me", async (ClaimsPrincipal caller, AppDbContext db) =>
        {
            var id = caller.GetUserId();
            if (id == null) return Results.Unauthorized();

            var user = await db.Users.FindAsync(id.Value);
            return user == null
                ? Results.Unauthorized() // Konto existiert nicht mehr: Token ist wertlos
                : Results.Ok(new AuthUser(user.Id, user.Name, user.Email));
        });
    }

    private static async Task<IResult> Register(
        RegisterRequest request, AppDbContext db, IPasswordHasher<User> hasher, JwtSettings jwt)
    {
        var name = (request.Name ?? string.Empty).Trim();
        var email = (request.Email ?? string.Empty).Trim();
        var password = request.Password ?? string.Empty;

        if (name.Length < 1 || name.Length > 50)
            return Results.BadRequest(new { error = "Der Name muss zwischen 1 und 50 Zeichen lang sein." });

        if (email.Length > 254 || !MailAddress.TryCreate(email, out var parsed) || parsed.Address != email)
            return Results.BadRequest(new { error = "Bitte gib eine gültige E-Mail-Adresse ein." });

        if (password.Length < MinPasswordLength || password.Length > MaxPasswordLength)
            return Results.BadRequest(new
            {
                error = $"Das Passwort muss zwischen {MinPasswordLength} und {MaxPasswordLength} Zeichen lang sein."
            });

        var emailLower = email.ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Email.ToLower() == emailLower))
            return Results.Conflict(new { error = EmailTaken });

        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = name,
            Email = email,
            LastActivityDate = DateTime.UtcNow
        };
        user.PasswordHash = hasher.HashPassword(user, password);
        db.Users.Add(user);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (
            ex.InnerException?.Message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase) == true)
        {
            // Zwei gleichzeitige Registrierungen mit derselben Adresse: der Datenbank-Index entscheidet.
            return Results.Conflict(new { error = EmailTaken });
        }

        return Results.Ok(CreateAuthResponse(user, jwt));
    }

    private static async Task<IResult> Login(
        LoginRequest request, AppDbContext db, IPasswordHasher<User> hasher, JwtSettings jwt)
    {
        var email = (request.Email ?? string.Empty).Trim();
        var password = request.Password ?? string.Empty;

        if (email.Length == 0 || password.Length == 0)
            return Results.BadRequest(new { error = "Bitte gib E-Mail und Passwort ein." });
        if (email.Length > 254 || password.Length > MaxPasswordLength)
            return Results.Json(new { error = InvalidCredentials }, statusCode: StatusCodes.Status401Unauthorized);

        var emailLower = email.ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == emailLower);

        // Immer einen Hash prüfen (auch bei unbekannter E-Mail), damit die Zeit nichts verrät.
        var knownHash = user?.PasswordHash;
        var result = hasher.VerifyHashedPassword(user ?? DummyUser, knownHash ?? DummyHash.Value, password);

        if (user == null || knownHash == null || result == PasswordVerificationResult.Failed)
            return Results.Json(new { error = InvalidCredentials }, statusCode: StatusCodes.Status401Unauthorized);

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = hasher.HashPassword(user, password);
            await db.SaveChangesAsync();
        }

        return Results.Ok(CreateAuthResponse(user, jwt));
    }

    private static AuthResponse CreateAuthResponse(User user, JwtSettings jwt)
    {
        var now = DateTime.UtcNow;
        var expires = now.Add(jwt.Lifetime);

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim("sub", user.Id.ToString()),
                new Claim("name", user.Name),
                new Claim("email", user.Email)
            }),
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            NotBefore = now,
            Expires = expires,
            SigningCredentials = new SigningCredentials(jwt.Key, SecurityAlgorithms.HmacSha256)
        };

        var token = new JsonWebTokenHandler().CreateToken(descriptor);
        return new AuthResponse(token, expires, new AuthUser(user.Id, user.Name, user.Email));
    }
}
