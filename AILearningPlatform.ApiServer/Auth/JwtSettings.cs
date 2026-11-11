using Microsoft.IdentityModel.Tokens;

namespace AILearningPlatform.ApiServer.Auth;

/// <summary>Einstellungen zum Signieren und Prüfen der Anmelde-Tokens (JWT, HS256).</summary>
public sealed record JwtSettings(SecurityKey Key, string Issuer, string Audience, TimeSpan Lifetime);
