using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using TecNM.Api.Data;
using TecNM.Api.Modules.Session;
using Xunit;

namespace TecNM.Session.Tests;

public sealed class SessionTests
{
    private static IConfiguration Configuration() => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["Session:FrontendUrl"] = "https://localhost:5173", ["Session:Issuer"] = "issuer",
        ["Session:Audience"] = "audience", ["Session:SigningKey"] = Convert.ToBase64String(new byte[32]),
        ["Session:Microsoft:TenantId"] = "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"
    }).Build();

    private static ClaimsPrincipal Principal(params (string Name, string Value)[] claims) =>
        new(new ClaimsIdentity(claims.Select(claim => new Claim(claim.Name, claim.Value))));

    [Fact]
    public async Task FirstLoginBindsVerifiedIdentityAndNeverReplacesIt()
    {
        var connection = Environment.GetEnvironmentVariable("SESSION_TEST_DATABASE")
            ?? throw new InvalidOperationException("Falta SESSION_TEST_DATABASE.");
        var options = new DbContextOptionsBuilder<TecNMDbContext>().UseNpgsql(connection).Options;
        await using var db = new TecNMDbContext(options);
        var user = new User { Id = Guid.NewGuid(), UserTypeId = Guid.Parse("00000000-0000-4000-8000-000000000004"),
            FirstNames = "Prueba", PaternalLastName = "Prueba", GoogleEmail = "binding@itsmva.edu.mx",
            MicrosoftEmail = "binding@monclova.tecnm.mx" };
        db.Add(user);
        await db.SaveChangesAsync();
        var tenant = "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa";
        var oid = "bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb";
        ClaimsPrincipal Google(string subject, string verified = "true") => Principal(("email", user.GoogleEmail!),
            ("hd", "itsmva.edu.mx"), ("email_verified", verified), ("sub", subject));
        ClaimsPrincipal Microsoft(string objectId, string acct = "0") => Principal(("email", user.MicrosoftEmail!),
            ("acct", acct), ("tid", tenant), ("oid", objectId));
        try
        {
            var service = new SessionService(db, Configuration());
            Assert.Null(await service.FindAuthorizedUserAsync("google", Google("unverified", "false"), default));
            Assert.Null(await service.FindAuthorizedUserAsync("microsoft", Principal(("email", user.MicrosoftEmail!),
                ("tid", "cccccccc-cccc-4ccc-8ccc-cccccccccccc"), ("oid", oid)), default));
            Assert.NotNull(await service.FindAuthorizedUserAsync("google", Google("bound-id"), default));
            Assert.NotNull(await service.FindAuthorizedUserAsync("microsoft", Principal(("email", user.MicrosoftEmail!), ("tid", tenant), ("oid", oid)), default));
            Assert.NotNull(await service.FindAuthorizedUserAsync("microsoft", Microsoft(oid, "1"), default));
            Assert.Null(await service.FindAuthorizedUserAsync("microsoft", Principal(("email", "no-registrado@monclova.tecnm.mx"), ("tid", tenant), ("oid", oid)), default));
            Assert.Null(await service.FindAuthorizedUserAsync("google", Google("replacement"), default));
            Assert.Null(await service.FindAuthorizedUserAsync("microsoft", Microsoft("cccccccc-cccc-4ccc-8ccc-cccccccccccc"), default));
            var saved = await db.Set<User>().AsNoTracking().SingleAsync(item => item.Id == user.Id);
            Assert.Equal("bound-id", saved.GoogleSubject);
            Assert.Equal(Guid.Parse(tenant), saved.MicrosoftTenantId);
            Assert.Equal(Guid.Parse(oid), saved.MicrosoftObjectId);

            var other = new User { Id = Guid.NewGuid(), UserTypeId = user.UserTypeId, FirstNames = "Otra",
                PaternalLastName = "Persona", GoogleEmail = "otherbinding@itsmva.edu.mx",
                MicrosoftEmail = "otherbinding@monclova.tecnm.mx" };
            db.Add(other);
            await db.SaveChangesAsync();
            try
            {
                var reused = Principal(("email", other.GoogleEmail!), ("hd", "itsmva.edu.mx"),
                    ("email_verified", "true"), ("sub", "bound-id"));
                Assert.Null(await service.FindAuthorizedUserAsync("google", reused, default));
                var reusedMicrosoft = Principal(("email", other.MicrosoftEmail!), ("acct", "0"), ("tid", tenant), ("oid", oid));
                Assert.Null(await service.FindAuthorizedUserAsync("microsoft", reusedMicrosoft, default));
            }
            finally { db.Remove(other); await db.SaveChangesAsync(); }
        }
        finally { db.Remove(user); await db.SaveChangesAsync(); }
    }

    [Fact]
    public async Task ConcurrentFirstLoginsBindOnlyOneIdentity()
    {
        var connection = Environment.GetEnvironmentVariable("SESSION_TEST_DATABASE")
            ?? throw new InvalidOperationException("Falta SESSION_TEST_DATABASE.");
        var options = new DbContextOptionsBuilder<TecNMDbContext>().UseNpgsql(connection).Options;
        await using var db = new TecNMDbContext(options);
        var user = new User { Id = Guid.NewGuid(), UserTypeId = Guid.Parse("00000000-0000-4000-8000-000000000004"),
            FirstNames = "Prueba", PaternalLastName = "Concurrente", GoogleEmail = "race@itsmva.edu.mx" };
        db.Add(user);
        await db.SaveChangesAsync();
        async Task<User?> Login(string subject)
        {
            await using var context = new TecNMDbContext(options);
            return await new SessionService(context, Configuration()).FindAuthorizedUserAsync("google",
                Principal(("email", user.GoogleEmail!), ("email_verified", "true"), ("hd", "itsmva.edu.mx"), ("sub", subject)), default);
        }
        try
        {
            var results = await Task.WhenAll(Login("race-one"), Login("race-two"));
            Assert.Single(results, result => result is not null);
        }
        finally { db.Remove(user); await db.SaveChangesAsync(); }
    }

    [Fact]
    public void CsrfRequiresExactTrustedOrigin()
    {
        using var db = new TecNMDbContext(new DbContextOptionsBuilder<TecNMDbContext>().UseNpgsql("Host=localhost;Database=unused").Options);
        var service = new SessionService(db, Configuration());
        Assert.True(service.IsTrustedOrigin("https://localhost:5173"));
        Assert.False(service.IsTrustedOrigin("https://localhost:5174"));
        Assert.False(service.IsTrustedOrigin("https://localhost:5173.evil.example"));
        Assert.False(service.IsTrustedOrigin(null));
    }

    [Fact]
    public async Task RegisteredIdentityAndSessionAreValidatedAgainstDatabase()
    {
        var connection = Environment.GetEnvironmentVariable("SESSION_TEST_DATABASE")
            ?? throw new InvalidOperationException("SESSION_TEST_DATABASE debe apuntar a una base aislada inicializada con database.sql.");
        await using var db = new TecNMDbContext(new DbContextOptionsBuilder<TecNMDbContext>().UseNpgsql(connection).Options);
        var user = new User
        {
            Id = Guid.NewGuid(), UserTypeId = Guid.Parse("00000000-0000-4000-8000-000000000003"),
            FirstNames = "Ana", PaternalLastName = "García", GoogleEmail = "ana@itsmva.edu.mx", GoogleSubject = "google-id",
            MicrosoftEmail = "ana@monclova.tecnm.mx", MicrosoftTenantId = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"),
            MicrosoftObjectId = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb")
        };
        db.Add(user);
        await db.SaveChangesAsync();
        var service = new SessionService(db, Configuration());
        try
        {
            var google = Principal(("email", " ANA@itsmva.edu.mx "), ("email_verified", "true"), ("hd", "itsmva.edu.mx"), ("sub", "google-id"));
            var microsoft = Principal(("email", "ana@monclova.tecnm.mx"), ("acct", "0"), ("tid", user.MicrosoftTenantId!.Value.ToString()), ("oid", user.MicrosoftObjectId!.Value.ToString()));
            var authorized = await service.FindAuthorizedUserAsync("google", google, default);
            Assert.NotNull(authorized);
            Assert.NotNull(await service.FindAuthorizedUserAsync("microsoft", microsoft, default));
            Assert.Null(await service.FindAuthorizedUserAsync("google", Principal(("email", "ana@itsmva.edu.mx"), ("email_verified", "true"), ("hd", "itsmva.edu.mx"), ("sub", "wrong")), default));
            Assert.Null(await service.FindAuthorizedUserAsync("google", Principal(("email", "ana@sub.itsmva.edu.mx"), ("email_verified", "true"), ("hd", "itsmva.edu.mx"), ("sub", "google-id")), default));
            Assert.NotNull(await service.FindAuthorizedUserAsync("microsoft", Principal(("email", "ana@monclova.tecnm.mx"), ("acct", "1"), ("tid", user.MicrosoftTenantId.Value.ToString()), ("oid", user.MicrosoftObjectId.Value.ToString())), default));
            Assert.NotNull(await service.FindAuthorizedUserAsync("microsoft", Principal(("email", "ana@monclova.tecnm.mx"), ("tid", user.MicrosoftTenantId.Value.ToString()), ("oid", user.MicrosoftObjectId.Value.ToString())), default));

            var token = service.CreateToken(authorized!, out var expires);
            var parsed = new JwtSecurityTokenHandler().ReadJwtToken(token);
            Assert.Equal(new[] { "aud", "exp", "iat", "iss", "sub", "user_type" }, parsed.Payload.Keys.Order().ToArray());
            Assert.Equal("docente", parsed.Payload["user_type"]);
            Assert.Equal(TimeSpan.FromHours(1), expires - DateTimeOffset.FromUnixTimeSeconds((long)parsed.Payload["iat"]));
            Assert.NotNull(await service.GetSessionAsync(token, default));
            Assert.Null(await service.GetSessionAsync(token + "x", default));
            var key = new SymmetricSecurityKey(new byte[32]);
            string Sign(string issuer, string audience, DateTime expiry) => new JwtSecurityTokenHandler().WriteToken(
                new JwtSecurityToken(issuer, audience, [new Claim("sub", user.Id.ToString())], expires: expiry,
                    signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)));
            Assert.Null(await service.GetSessionAsync(Sign("issuer", "audience", DateTime.UtcNow.AddMinutes(-1)), default));
            Assert.Null(await service.GetSessionAsync(Sign("other-issuer", "audience", DateTime.UtcNow.AddMinutes(1)), default));
            Assert.Null(await service.GetSessionAsync(Sign("issuer", "other-audience", DateTime.UtcNow.AddMinutes(1)), default));
            user.UserTypeId = Guid.Parse("00000000-0000-4000-8000-000000000002");
            await db.SaveChangesAsync();
            Assert.Equal("admin", (await service.GetSessionAsync(token, default))!.User.UserType.Code);
            db.Remove(user);
            await db.SaveChangesAsync();
            Assert.Null(await service.GetSessionAsync(token, default));
        }
        finally
        {
            if (await db.Set<User>().AnyAsync(item => item.Id == user.Id)) { db.Remove(user); await db.SaveChangesAsync(); }
        }
    }
}
