using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SapApi.Domain.Interfaces;
using SapApi.Infrastructure.Services;
using SapApi.Shared.Models;
using SapApi.Shared.Requests.Account;

namespace SapApi.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(AuthService authService, IRsaDecryptionService rsa) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("public-key")]
    public IActionResult GetPublicKey() =>
        Ok(ApiResponse<object>.Ok(new { publicKey = rsa.GetPublicKeyPem() }));

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.LoginAsync(request, cancellationToken);
        return result.Success ? Ok(result) : Unauthorized(result);
    }

    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterUserRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.RegisterAsync(request, cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.RefreshAsync(request, cancellationToken);
        return result.Success ? Ok(result) : Unauthorized(result);
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var result = await authService.LogoutAsync(cancellationToken);
        return Ok(result);
    }
}
