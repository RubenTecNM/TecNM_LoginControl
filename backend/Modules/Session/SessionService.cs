using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using TecNM.Api.Data;

namespace TecNM.Api.Modules.Session;

public sealed class SessionService(TecNMDbContext db, IConfiguration configuration)
{
    public const string CookieName = "tecnm_session";

    public string? FrontendUrl => configuration["Session:FrontendUrl"]?.TrimEnd('/');

    public bool IsConfigured(string provider)
    {
        var section = configuration.GetSection($"Session:{provider}");
        return !string.IsNullOrWhiteSpace(section["ClientId"])
            && !string.IsNullOrWhiteSpace(section["ClientSecret"])
            && (provider != "Microsoft" || Guid.TryParse(section["TenantId"], out _))
            && TrySettings(out _, out _, out _)
            && Uri.TryCreate(FrontendUrl, UriKind.Absolute, out var url)
            && url.Scheme == Uri.UriSchemeHttps && url.AbsolutePath == "/"
            && string.IsNullOrEmpty(url.Query) && string.IsNullOrEmpty(url.Fragment);
    }

    public bool IsTrustedOrigin(string? origin)
    {
        return Uri.TryCreate(FrontendUrl, UriKind.Absolute, out var frontend)
            && Uri.TryCreate(origin, UriKind.Absolute, out var requestOrigin)
            && requestOrigin.AbsolutePath == "/" && string.IsNullOrEmpty(requestOrigin.Query)
            && string.IsNullOrEmpty(requestOrigin.Fragment)
            && string.Equals(frontend.GetLeftPart(UriPartial.Authority), requestOrigin.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase);
    }

    public async Task<User?> FindAuthorizedUserAsync(string provider, ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        var email = principal.FindFirst("email")?.Value?.Trim();
        if (string.IsNullOrWhiteSpace(email)) return null;
        var normalizedEmail = email.ToLowerInvariant();
        var parts = normalizedEmail.Split('@');
        if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0])) return null;

        var users = db.Set<User>().Include(user => user.UserType).AsNoTracking();
        if (provider == "google")
        {
            var subject = principal.FindFirst("sub")?.Value;
            if (parts[1] != "itsmva.edu.mx" || principal.FindFirst("hd")?.Value != "itsmva.edu.mx"
                || !string.Equals(principal.FindFirst("email_verified")?.Value, "true", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(subject) || subject.Length > 255) return null;
            try
            {
                await db.Set<User>().Where(user => user.GoogleEmail != null && user.GoogleEmail.ToLower() == normalizedEmail
                    && user.GoogleSubject == null)
                    .ExecuteUpdateAsync(update => update.SetProperty(user => user.GoogleSubject, subject), cancellationToken);
            }
            catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation) { return null; }
            return await users.SingleOrDefaultAsync(user => user.GoogleSubject == subject
                && user.GoogleEmail != null && user.GoogleEmail.ToLower() == normalizedEmail, cancellationToken);
        }

        if (provider == "microsoft")
        {
            if (parts[1] != "monclova.tecnm.mx"
                || !Guid.TryParse(principal.FindFirst("tid")?.Value, out var tenant)
                || !Guid.TryParse(configuration["Session:Microsoft:TenantId"], out var allowedTenant)
                || tenant != allowedTenant || !Guid.TryParse(principal.FindFirst("oid")?.Value, out var objectId)) return null;
            try
            {
                await db.Set<User>().Where(user => user.MicrosoftEmail != null && user.MicrosoftEmail.ToLower() == normalizedEmail
                    && user.MicrosoftTenantId == null && user.MicrosoftObjectId == null)
                    .ExecuteUpdateAsync(update => update.SetProperty(user => user.MicrosoftTenantId, tenant)
                        .SetProperty(user => user.MicrosoftObjectId, objectId), cancellationToken);
            }
            catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation) { return null; }
            return await users.SingleOrDefaultAsync(user => user.MicrosoftTenantId == tenant && user.MicrosoftObjectId == objectId
                && user.MicrosoftEmail != null && user.MicrosoftEmail.ToLower() == normalizedEmail, cancellationToken);
        }
        return null;
    }

    public string CreateToken(User user, out DateTimeOffset expiresAt)
    {
        if (!TrySettings(out var key, out var issuer, out var audience)) throw new InvalidOperationException("Configuración de sesión incompleta.");
        var now = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        expiresAt = now.AddHours(1);
        var token = new JwtSecurityToken(issuer, audience,
            [new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()), new Claim("user_type", user.UserType.Code),
             new Claim(JwtRegisteredClaimNames.Iat, now.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)],
            now.UtcDateTime, expiresAt.UtcDateTime, new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        // Do not emit nbf: the application contract has only the five registered claims plus user_type.
        token.Payload.Remove(JwtRegisteredClaimNames.Nbf);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public async Task<SessionResponse?> GetSessionAsync(string? token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token) || !TrySettings(out var key, out var issuer, out var audience)) return null;
        try
        {
            var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
            var principal = handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = true, ValidIssuer = issuer, ValidateAudience = true, ValidAudience = audience,
                ValidateIssuerSigningKey = true, IssuerSigningKey = key, RequireSignedTokens = true,
                ValidAlgorithms = [SecurityAlgorithms.HmacSha256], ValidateLifetime = true, RequireExpirationTime = true,
                ClockSkew = TimeSpan.Zero
            }, out var validatedToken);
            if (!Guid.TryParse(principal.FindFirst("sub")?.Value, out var id)) return null;
            var user = await db.Set<User>().Include(item => item.UserType).AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (user is null) return null;
            return new SessionResponse(new SessionUser(user.Id, user.FirstNames, user.PaternalLastName, user.MaternalLastName,
                new SessionUserType(user.UserType.Code, user.UserType.Name)), new DateTimeOffset(validatedToken.ValidTo, TimeSpan.Zero));
        }
        catch (SecurityTokenException) { return null; }
        catch (ArgumentException) { return null; }
    }

    private bool TrySettings(out SymmetricSecurityKey key, out string issuer, out string audience)
    {
        issuer = configuration["Session:Issuer"] ?? "";
        audience = configuration["Session:Audience"] ?? "";
        key = null!;
        try
        {
            var bytes = Convert.FromBase64String(configuration["Session:SigningKey"] ?? "");
            if (bytes.Length < 32 || string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(audience)) return false;
            key = new SymmetricSecurityKey(bytes);
            return true;
        }
        catch (FormatException) { return false; }
    }
}

public sealed record SessionUserType(string Code, string Name);
public sealed record SessionUser(Guid Id, string FirstNames, string PaternalLastName, string? MaternalLastName, SessionUserType UserType);
public sealed record SessionResponse(SessionUser User, DateTimeOffset ExpiresAt);
