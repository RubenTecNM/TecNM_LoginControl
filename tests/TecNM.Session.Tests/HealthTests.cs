using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TecNM.Api.Data;
using TecNM.Api.Modules.Session;
using Xunit;

namespace TecNM.Session.Tests;

public sealed class HealthTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UnconfiguredProvidersDoNotBreakHealthAndLoginReturns503(string? credential)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Production
        });
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Session:Google:ClientId"] = credential,
            ["Session:Google:ClientSecret"] = credential,
            ["Session:Microsoft:ClientId"] = credential,
            ["Session:Microsoft:ClientSecret"] = credential
        });
        builder.Services.AddControllers().AddApplicationPart(typeof(SessionController).Assembly);
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        builder.Services.AddSessionAuthentication(builder.Configuration);
        builder.Services.AddDbContext<TecNMDbContext>(options =>
            options.UseNpgsql("Host=127.0.0.1;Port=1;Database=unused;Timeout=1"));
        await using var app = builder.Build();
        app.Urls.Add("http://127.0.0.1:0");
        app.UseAuthentication();
        app.MapGet("/api/health", () => new { status = "ok" });
        app.MapControllers();
        await app.StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        using var health = await client.GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        Assert.Contains("\"status\":\"ok\"", await health.Content.ReadAsStringAsync());
        foreach (var provider in new[] { "google", "microsoft" })
        {
            using var login = await client.GetAsync($"/api/session/login/{provider}");
            Assert.Equal(HttpStatusCode.ServiceUnavailable, login.StatusCode);
        }
    }
}
