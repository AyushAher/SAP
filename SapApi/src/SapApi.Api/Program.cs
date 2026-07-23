using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SapApi.Api.DevTools;
using SapApi.Api.Hangfire;
using SapApi.Api.Middleware;
using SapApi.Infrastructure;
using SapApi.Infrastructure.Persistence;
using SapApi.Domain.Entities;
using Serilog;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);
Console.WriteLine($"SapApi starting (env={builder.Environment.EnvironmentName})");

var cliExitCode = await CancelStageWisePaymentsCommand.TryRunAsync(args, builder.Configuration);
if (cliExitCode >= 0)
    Environment.Exit(cliExitCode);

builder.Host.UseSerilog((ctx, cfg) =>
    cfg.ReadFrom.Configuration(ctx.Configuration).WriteTo.Console());

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.Converters.Add(new UtcDateTimeJsonConverter());
        options.JsonSerializerOptions.Converters.Add(new UtcNullableDateTimeJsonConverter());
        // EF Core performs bidirectional navigation fixup during query materialization (even with
        // AsNoTracking) whenever related entities appear together in one query's Include graph
        // (e.g. ApprovalRequest -> UserApprovals -> ApprovalRequest, or ApplicationUser's inverse
        // collections). Several endpoints serialize domain entities directly, so a reference cycle
        // here must degrade to `null` instead of throwing and returning a 500.
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<SapApi.Infrastructure.Services.AuthService>();
builder.Services.AddSapHangfire(builder.Configuration);
builder.Services.AddHealthChecks();

builder.Services.AddCors(options =>
{
    options.AddPolicy("SapUi", policy =>
        policy.WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:5173"])
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});

var app = builder.Build();

try
{
    using (var scope = app.Services.CreateScope())
    {
        DependencyInjection.InitializeEncryption(scope.ServiceProvider);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (app.Environment.IsEnvironment("Testing"))
            await db.Database.EnsureCreatedAsync();
        else
            await db.Database.MigrateAsync();

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        await IdentityDataSeeder.SeedRolesAsync(roleManager);
    }
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application startup failed during database initialization");
    throw;
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseCors("SapUi");
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<SecurityMiddleware>();
app.UseSapHangfire();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

public partial class Program;
