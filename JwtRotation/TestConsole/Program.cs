using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RotatingJwt;
using System.Security.Claims;


var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddRotatingJwt(context.Configuration);
    })
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders();
        logging.AddConsole();
    })
    .Build();

var jwtService = host.Services.GetRequiredService<IJwtTokenService>();
var additionalClaims = new List<Claim>
{
    new(ClaimTypes.NameIdentifier, "10"),
    new(ClaimTypes.Name, "sudarshan.n@jisasoftech.com"),
    new(ClaimTypes.GivenName, "sudarshan.n@jisasoftech.com"),
    new(ClaimTypes.Surname, "Sudarshan Narhe"),
    new(ClaimTypes.Actor, "9846789547"),
    new(ClaimTypes.PrimarySid, "0")
};

var tokenResponse = jwtService.GenerateAccessToken("4", additionalClaims, true).Result;
Console.WriteLine($"Generated JWT: {tokenResponse.Token}");
Console.WriteLine($"Generated Pub Key: {JwtTokenService.ConvertEncKeyToPem(tokenResponse.PublicKey)}");

Console.ReadKey();