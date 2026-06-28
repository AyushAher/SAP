using SapApi;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File("Logs/Log.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

try
{
    WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
    builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

    builder.Services.AddControllers();
    builder.Services.AddOpenApi();
    builder.Services.AddMemoryCache();
    builder.Services.AddServices(builder.Configuration);
    builder.Services.AddAuthenticationConfigServices();
    builder.Host.UseSerilog(Log.Logger);

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
    WebApplication app = builder.Build();

    app.UseSwagger();
    app.UseSwaggerUI();
    app.MapOpenApi();
    app.UseHttpsRedirection();
    app.UseStaticFiles();
    app.UseRouting();
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseSerilogRequestLogging();
    app.MapControllers();
    string? sapPassword = Environment.GetEnvironmentVariable("SAP_PASSWORD");

    if (string.IsNullOrWhiteSpace(sapPassword))
    {
        throw new InvalidOperationException("SAP_PASSWORD is not set");
    }
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "An exception occured {Exception}.", ex);
}