using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.WebUtilities;
using TecNM.Api.Data;
using TecNM.Api.Modules.ModuleAccess;
using Xunit;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using TecNM.Api.Modules.Session;

namespace TecNM.Session.Tests;

public sealed class ModuleTests
{
    private static TecNMDbContext Database() => new(new DbContextOptionsBuilder<TecNMDbContext>()
        .UseNpgsql(Environment.GetEnvironmentVariable("SESSION_TEST_DATABASE") ?? throw new InvalidOperationException("Falta base aislada" )).Options);

    [Fact]
    public async Task AdministrationChecksCurrentRoleAndOrigin()
    {
        await using var db = Database();
        var user = new User { Id = Guid.NewGuid(), UserTypeId = Guid.Parse("00000000-0000-4000-8000-000000000001"), FirstNames = "Admin", PaternalLastName = "Prueba", GoogleEmail = $"{Guid.NewGuid():N}@itsmva.edu.mx" };
        db.Add(user); await db.SaveChangesAsync();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
            ["Session:FrontendUrl"] = "https://localhost:5173", ["Session:Issuer"] = "issuer", ["Session:Audience"] = "audience", ["Session:SigningKey"] = Convert.ToBase64String(new byte[32]) }).Build();
        var sessions = new SessionService(db, configuration);
        var controller = new ModuleAdminController(new ModuleAdminService(db),sessions) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        Assert.IsType<UnauthorizedResult>(await controller.List(default));
        var loaded = await db.Set<User>().Include(x => x.UserType).SingleAsync(x => x.Id == user.Id);
        var token = sessions.CreateToken(loaded, out _);
        controller.Request.Headers.Cookie = $"{SessionService.CookieName}={token}";
        Assert.IsType<OkObjectResult>(await controller.List(default));
        Assert.Equal(403, ((StatusCodeResult)await controller.Create(new("X","https://example.test",true),default)).StatusCode);
        controller.Request.Headers.Origin = "https://localhost:5173";
        Assert.IsType<OkObjectResult>(await controller.Create(new("X","https://example.test",true),default));
        loaded.UserTypeId = Guid.Parse("00000000-0000-4000-8000-000000000002"); await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        Assert.Equal(403, ((StatusCodeResult)await controller.List(default)).StatusCode);
    }

    [Fact]
    public void MutationsRequireExplicitActiveField()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ModuleInput>("{\"name\":\"X\",\"entryUrl\":\"https://example.test\"}", options));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<AssignmentInput>("{}", options));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<AssignmentBatch>("{\"mode\":\"all\"}", options));
    }

    [Fact]
    public async Task AssignmentAndExchangeEnforceCurrentPermissionsAndSingleUse()
    {
        await using var db = Database();
        var user = new User { Id = Guid.NewGuid(), UserTypeId = Guid.Parse("00000000-0000-4000-8000-000000000004"), FirstNames = "Prueba", PaternalLastName = "Módulo", GoogleEmail = $"{Guid.NewGuid():N}@itsmva.edu.mx" };
        db.Add(user); await db.SaveChangesAsync();
        var admin = new ModuleAdminService(db);
        var module = await admin.CreateAsync(new("Prueba", "https://example.test/receive", true), default);
        var exchange = new ModuleExchangeService(db);
        Assert.Empty(await exchange.ListAsync(user.Id, default));
        Assert.Equal(403, (await Assert.ThrowsAsync<ModuleAccessException>(() => exchange.LaunchAsync(user.Id, module.Module.Id, default))).StatusCode);
        Assert.Equal(new BatchResult(1,0,0), await admin.BulkAsync(module.Module.Id, new("selected", [user.Id,user.Id], true), default));
        Assert.Equal(new BatchResult(0,0,1), await admin.SetAssignmentAsync(user.Id,module.Module.Id,true,default));
        Assert.Equal(400, (await Assert.ThrowsAsync<ModuleAccessException>(() => admin.BulkAsync(module.Module.Id, new("selected", [user.Id,Guid.NewGuid()], false), default))).StatusCode);
        Assert.Single(await exchange.ListAsync(user.Id, default));
        await admin.BulkAsync(module.Module.Id, new("all", null, true), default);
        var later = new User { Id = Guid.NewGuid(), UserTypeId = user.UserTypeId, FirstNames = "Nueva", PaternalLastName = "Persona", GoogleEmail = $"{Guid.NewGuid():N}@itsmva.edu.mx" };
        db.Add(later); await db.SaveChangesAsync();
        Assert.Empty(await exchange.ListAsync(later.Id, default));
        async Task<string> Launch()
        {
            var ticket = QueryHelpers.ParseQuery(new Uri(await exchange.LaunchAsync(user.Id,module.Module.Id,default)).Query)["ticket"].ToString();
            Assert.Equal(400, (await Assert.ThrowsAsync<ModuleAccessException>(() => exchange.AuthorizeAsync(Guid.NewGuid(),ticket,new string('a',32),default))).StatusCode);
            return QueryHelpers.ParseQuery(new Uri(await exchange.AuthorizeAsync(user.Id,ticket,new string('a',32),default)).Query)["code"].ToString();
        }
        var code = await Launch();
        Assert.Equal(401, (await Assert.ThrowsAsync<ModuleAccessException>(() => exchange.ExchangeAsync(module.Module.Id,"invalid",code,default))).StatusCode);
        var result = JsonSerializer.Serialize(await exchange.ExchangeAsync(module.Module.Id,module.ApiKey,code,default));
        using var json = JsonDocument.Parse(result);
        Assert.Equal(user.Id, json.RootElement.GetProperty("user").GetProperty("Id").GetGuid());
        Assert.Equal(6, json.RootElement.GetProperty("user").EnumerateObject().Count());
        Assert.Equal(400, (await Assert.ThrowsAsync<ModuleAccessException>(() => exchange.ExchangeAsync(module.Module.Id,module.ApiKey,code,default))).StatusCode);
        code = await Launch();
        var concurrentCode = code;
        async Task<bool> Redeem()
        {
            await using var context = Database();
            try { await new ModuleExchangeService(context).ExchangeAsync(module.Module.Id,module.ApiKey,concurrentCode,default); return true; }
            catch (ModuleAccessException) { return false; }
        }
        Assert.Single(await Task.WhenAll(Redeem(), Redeem()), success => success);
        code = await Launch();
        var pending = await db.Set<ModuleAuthToken>().SingleAsync(t => t.TokenHash == ModuleAdminService.Hash(code));
        pending.CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-2); pending.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1); await db.SaveChangesAsync();
        Assert.Equal(400, (await Assert.ThrowsAsync<ModuleAccessException>(() => exchange.ExchangeAsync(module.Module.Id,module.ApiKey,code,default))).StatusCode);
        code = await Launch();
        await admin.SetAssignmentAsync(user.Id,module.Module.Id,false,default);
        Assert.Equal(403, (await Assert.ThrowsAsync<ModuleAccessException>(() => exchange.ExchangeAsync(module.Module.Id,module.ApiKey,code,default))).StatusCode);
        await admin.SetAssignmentAsync(user.Id,module.Module.Id,true,default);
        var key = await admin.RotateAsync(module.Module.Id,default);
        Assert.Equal(401, (await Assert.ThrowsAsync<ModuleAccessException>(() => exchange.ExchangeAsync(module.Module.Id,module.ApiKey,code,default))).StatusCode);
        Assert.Equal(400, (await Assert.ThrowsAsync<ModuleAccessException>(() => exchange.ExchangeAsync(module.Module.Id,key,code,default))).StatusCode);
    }
}
