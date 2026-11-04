using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace TecNM.Api.Modules.Session;

public static class SessionAuthentication
{
    public static void AddSessionAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<SessionService>();
        var authentication = services.AddAuthentication().AddCookie("oidc-temporary");
        AddProvider(authentication, configuration, "Google", "google", "https://accounts.google.com");
        var tenant = Guid.TryParse(configuration["Session:Microsoft:TenantId"], out var id) ? id.ToString() : Guid.Empty.ToString();
        AddProvider(authentication, configuration, "Microsoft", "microsoft", $"https://login.microsoftonline.com/{tenant}/v2.0");
    }

    private static void AddProvider(Microsoft.AspNetCore.Authentication.AuthenticationBuilder authentication, IConfiguration configuration,
        string scheme, string provider, string authority)
    {
        var clientId = configuration[$"Session:{scheme}:ClientId"];
        var clientSecret = configuration[$"Session:{scheme}:ClientSecret"];
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            return;
        }

        authentication.AddOpenIdConnect(scheme, options =>
        {
            options.Authority = authority;
            options.ClientId = clientId;
            options.ClientSecret = clientSecret;
            options.CallbackPath = $"/api/session/callback/{provider}";
            options.SignInScheme = "oidc-temporary";
            options.ResponseType = OpenIdConnectResponseType.Code;
            options.ResponseMode = OpenIdConnectResponseMode.Query;
            options.UsePkce = true;
            options.MapInboundClaims = false;
            options.SaveTokens = false;
            options.Scope.Clear();
            options.Scope.Add("openid");
            options.Scope.Add("email");
            if (provider == "microsoft") options.Scope.Add("profile");
            options.TokenValidationParameters = new TokenValidationParameters { NameClaimType = "sub" };
            options.Events.OnTicketReceived = async context =>
            {
                context.HandleResponse();
                context.Response.Headers.CacheControl = "no-store";
                var session = context.HttpContext.RequestServices.GetRequiredService<SessionService>();
                var redirect = session.FrontendUrl ?? "/";
                try
                {
                    if (!session.IsConfigured(scheme) || context.Principal is null)
                    {
                        context.Response.Redirect(redirect + "?session=error");
                        return;
                    }
                    var user = await session.FindAuthorizedUserAsync(provider, context.Principal, context.HttpContext.RequestAborted);
                    if (user is null)
                    {
                        context.Response.Redirect(redirect + "?session=error");
                        return;
                    }
                    var token = session.CreateToken(user, out var expiresAt);
                    var cookie = CookieOptions();
                    cookie.Expires = expiresAt;
                    context.Response.Cookies.Append(SessionService.CookieName, token, cookie);
                    context.Response.Redirect(redirect + "/");
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    var logger = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("SessionAuthentication");
                    logger.LogError("Error interno al finalizar el inicio de sesión ({ErrorType}).", exception.GetType().Name);
                    context.Response.Redirect(redirect + "?session=error");
                }
            };
            options.Events.OnRemoteFailure = context =>
            {
                context.HandleResponse();
                context.Response.Headers.CacheControl = "no-store";
                var session = context.HttpContext.RequestServices.GetRequiredService<SessionService>();
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("SessionAuthentication");
                logger.LogWarning("Falló el flujo de autenticación del proveedor ({ErrorType}).", context.Failure?.GetType().Name);
                context.Response.Redirect((session.FrontendUrl ?? "/") + "?session=error");
                return Task.CompletedTask;
            };
        });
    }

    public static CookieOptions CookieOptions() => new()
    {
        HttpOnly = true, Secure = true, SameSite = SameSiteMode.Lax, Path = "/"
    };
}
