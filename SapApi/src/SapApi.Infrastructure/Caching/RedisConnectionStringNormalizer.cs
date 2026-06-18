using System.Text.RegularExpressions;

namespace SapApi.Infrastructure.Caching;

internal static partial class RedisConnectionStringNormalizer
{
    private static readonly Regex AbortConnectRegex = AbortConnectPattern();

    internal static string? Normalize(string? connection)
    {
        if (string.IsNullOrWhiteSpace(connection))
            return null;

        connection = connection.Trim().Trim('"', '\'');

        var match = AbortConnectRegex.Match(connection);
        if (match.Success)
        {
            var abortConnect = match.Groups[1].Value.Trim().Trim('"', '\'');
            if (abortConnect is not ("true" or "false"))
            {
                throw new InvalidOperationException(
                    $"Redis connection string has invalid abortConnect value '{abortConnect}'. " +
                    "Use abortConnect=true or abortConnect=false without quotes.");
            }

            connection = connection[..match.Groups[1].Index] + abortConnect + connection[(match.Groups[1].Index + match.Groups[1].Length)..];
        }

        if (connection.Contains('"') || connection.Contains('\''))
        {
            throw new InvalidOperationException(
                "Redis connection string contains quote characters. " +
                "Use e.g. redis:6379,abortConnect=false with no surrounding quotes.");
        }

        return connection;
    }

    [GeneratedRegex(@"abortConnect=([^,;]+)", RegexOptions.IgnoreCase)]
    private static partial Regex AbortConnectPattern();
}
