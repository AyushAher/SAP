using Hangfire;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.HttpOverrides;
using MudBlazor;
using MudBlazor.Services;
using RamsonDevelopers.UtilEmail;
using SapForm;
using SapForm.Components;
using Serilog;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);


Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File("Logs/Log.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

GlobalConfiguration.Configuration
    .UseSerilogLogProvider();
try
{

    builder.Services.AddSerilog();
    builder.Services.AddMudServices(config =>
    {
        config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.TopRight;
    });

    builder.Services.AddMemoryCache();

    // Add services to the container.
    builder.Services.AddRazorComponents()
        .AddInteractiveServerComponents();
    builder.Services.AddControllers();

    builder.Services.AddServices(builder.Configuration);
    builder.Host.UseSerilog(Log.Logger);

    IConfiguration configuration = builder.Configuration;
    IConfigurationSection emailConfig = configuration.GetSection(EmailConfig.SectionLabel);
    builder.Services.Configure<EmailConfig>(emailConfig);
    builder.Services.AddEmailService();
    builder.Services.AddScoped(sp =>
    {
        NavigationManager nav = sp.GetRequiredService<NavigationManager>();
        return new HttpClient
        {
            BaseAddress = new Uri(nav.BaseUri)
        };
    });
    builder.Services.AddResponseCompression();

    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders =
            ForwardedHeaders.XForwardedFor |
            ForwardedHeaders.XForwardedProto;
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();
    });

    WebApplication app = builder.Build();

    // Configure the HTTP request pipeline.
    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error", createScopeForErrors: true);
        app.UseHsts();
        app.UseHttpsRedirection();
    }


    app.UseForwardedHeaders();
    app.UseStaticFiles();
    app.MapStaticAssets();

    app.UseRouting();

    app.UseAuthentication();
    app.UseAuthorization();
    app.UseAntiforgery();

    app.UseSerilogRequestLogging();

    app.MapControllers();
    app.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode();
    app.UseHangfireDashboard("/hangfire");

    var sapPassword = Environment.GetEnvironmentVariable("SAP_PASSWORD");

    if (string.IsNullOrWhiteSpace(sapPassword))
    {
        throw new InvalidOperationException("SAP_PASSWORD is not set");
    }
    app.UseMiddleware<GlobalExceptionMiddleware>();
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "An exception occured {Exception}.", ex);
}