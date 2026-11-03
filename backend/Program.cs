using Microsoft.EntityFrameworkCore;
using TecNM.Api.Data;
using TecNM.Api.Modules.Session;

const string FrontendCors = "Frontend";

LoadDotEnv();

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;

// Los callbacks OIDC transportan códigos temporales en la cadena de consulta.
builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning);

var connectionString = configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Falta ConnectionStrings__DefaultConnection en la configuración.");
}

var allowedOrigins = (configuration["Cors:AllowedOrigins"] ?? configuration["Cors:AllowedOrigin"])
    ?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    .Select(origin => origin.TrimEnd('/'))
    .ToArray();

if (allowedOrigins is null || allowedOrigins.Length == 0)
{
    if (builder.Environment.IsDevelopment())
    {
        allowedOrigins = ["https://localhost:5173", "http://localhost:5173"];
    }
    else
    {
        throw new InvalidOperationException(
            "Cors__AllowedOrigin o Cors__AllowedOrigins es obligatorio en producción.");
    }
}

foreach (var origin in allowedOrigins)
{
    if (!Uri.TryCreate(origin, UriKind.Absolute, out var originUri)
        || (!builder.Environment.IsDevelopment() && originUri.Scheme != Uri.UriSchemeHttps))
    {
        throw new InvalidOperationException(
            builder.Environment.IsDevelopment()
                ? $"El origen CORS '{origin}' debe ser una URI absoluta válida."
                : $"El origen CORS '{origin}' debe ser un origen HTTPS absoluto.");
    }
}

builder.Services.AddControllers();
builder.Services.AddSessionAuthentication(configuration);
builder.Services.AddDbContext<TecNMDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddCors(options => options.AddPolicy(FrontendCors, policy =>
    policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

var app = builder.Build();

app.UseHttpsRedirection();
app.UseCors(FrontendCors);
app.UseAuthentication();
app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/api/health/database", async (TecNMDbContext db, CancellationToken cancellationToken) =>
{
    try
    {
        return await db.Database.CanConnectAsync(cancellationToken)
            ? Results.Ok(new { status = "ok" })
            : Results.Json(new { status = "error" }, statusCode: 503);
    }
    catch (Exception)
    {
        return Results.Json(new { status = "error" }, statusCode: 503);
    }
});
app.MapControllers();

app.Run();

static void LoadDotEnv()
{
    var searchDir = new DirectoryInfo(Directory.GetCurrentDirectory());
    DirectoryInfo? backendDir = null;

    if (File.Exists(Path.Combine(searchDir.FullName, "TecNM.Api.csproj")))
    {
        backendDir = searchDir;
    }
    else if (Directory.Exists(Path.Combine(searchDir.FullName, "backend")))
    {
        backendDir = new DirectoryInfo(Path.Combine(searchDir.FullName, "backend"));
    }
    else
    {
        var baseDir = new DirectoryInfo(AppContext.BaseDirectory);
        while (baseDir != null)
        {
            if (File.Exists(Path.Combine(baseDir.FullName, "TecNM.Api.csproj")))
            {
                backendDir = baseDir;
                break;
            }

            baseDir = baseDir.Parent;
        }
    }

    if (backendDir == null)
    {
        return;
    }

    var envPath = Path.Combine(backendDir.FullName, ".env");
    if (!File.Exists(envPath))
    {
        return;
    }

    foreach (var rawLine in File.ReadAllLines(envPath))
    {
        var line = rawLine.Trim();
        if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
        {
            continue;
        }

        var separatorIndex = line.IndexOf('=');
        if (separatorIndex <= 0)
        {
            continue;
        }

        var key = line[..separatorIndex].Trim();
        var value = line[(separatorIndex + 1)..].Trim().Trim('"', '\'');

        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
        {
            Environment.SetEnvironmentVariable(key, value);
        }
    }
}
