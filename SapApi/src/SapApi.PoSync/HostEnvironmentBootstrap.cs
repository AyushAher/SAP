using Microsoft.Extensions.Configuration;

namespace SapApi.PoSync;

/// <summary>
/// Maps host/deploy env names (same as docker-compose.uat.yml) onto ASP.NET configuration keys.
/// Also loads an optional local <c>.env</c> file next to the executable / project.
/// </summary>
internal static class HostEnvironmentBootstrap
{
    private static readonly string[] EnvFileNames = [".env", ".env.local"];

    public static void Apply(ConfigurationManager configuration)
    {
        LoadDotEnvFiles();

        MapIfPresent(configuration, "DB_CONNECTION", "ConnectionStrings:DbConnection", NormalizeDbConnection);
        MapIfPresent(configuration, "REDIS_CONNECTION", "ConnectionStrings:RedisConnection");
        MapIfPresent(configuration, "SAP_SERVICE_LAYER_URL", "ApplicationConfiguration:SapServiceLayerUrl");
        MapIfPresent(configuration, "SAP_USERNAME", "SapCredentials:Accounts:0:Username");
        MapIfPresent(configuration, "SAP_PASSWORD", "SapCredentials:Accounts:0:Password");
        MapIfPresent(configuration, "SAP_COMPANY_DB", "SapCredentials:Accounts:0:CompanyDb");
        MapIfPresent(configuration, "SAP_COMPANY_DB", "PurchaseOrderSync:CompanyDb");
    }

    private static void LoadDotEnvFiles()
    {
        foreach (var dir in CandidateDirectories())
        {
            foreach (var name in EnvFileNames)
            {
                var path = Path.Combine(dir, name);
                if (!File.Exists(path))
                    continue;

                foreach (var raw in File.ReadLines(path))
                {
                    var line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith('#') || !line.Contains('='))
                        continue;

                    var sep = line.IndexOf('=');
                    var key = line[..sep].Trim();
                    var value = StripQuotes(line[(sep + 1)..].Trim());
                    if (key.Length == 0)
                        continue;

                    // Do not override variables already set in the process environment.
                    if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
                        Environment.SetEnvironmentVariable(key, value);
                }

                return;
            }
        }
    }

    private static IEnumerable<string> CandidateDirectories()
    {
        yield return Directory.GetCurrentDirectory();
        yield return AppContext.BaseDirectory;

        // Walk up from the bin folder to the project root during `dotnet run`.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 6 && dir is not null; i++, dir = dir.Parent)
            yield return dir.FullName;
    }

    private static void MapIfPresent(
        ConfigurationManager configuration,
        string envName,
        string configKey,
        Func<string, string>? normalize = null)
    {
        var value = Environment.GetEnvironmentVariable(envName);
        if (string.IsNullOrWhiteSpace(value))
            return;

        configuration[configKey] = normalize?.Invoke(value) ?? value;
    }

    /// <summary>
    /// Host .env files sometimes omit the Npgsql <c>Host=</c> prefix
    /// (e.g. <c>192.168.0.5;Port=5432;...</c>).
    /// </summary>
    internal static string NormalizeDbConnection(string connection)
    {
        var value = connection.Trim().Trim('"');
        if (!value.Contains("Host=", StringComparison.OrdinalIgnoreCase)
            && !value.Contains("Server=", StringComparison.OrdinalIgnoreCase))
        {
            value = "Host=" + value;
        }

        return value;
    }

    private static string StripQuotes(string value)
    {
        if (value.Length >= 2
            && ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')))
        {
            return value[1..^1];
        }

        return value;
    }
}
