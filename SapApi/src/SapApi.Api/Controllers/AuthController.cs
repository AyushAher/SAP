using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SapApi.Domain.Interfaces;
using SapApi.Infrastructure.Identity;
using SapApi.Infrastructure.Services;
using SapApi.Infrastructure.Services.Sap;
using SapApi.Shared.Models;
using SapApi.Shared.Requests.Account;

namespace SapApi.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(
    AuthService authService,
    SapMasterDataService masterDataService,
    IRsaDecryptionService rsa,
    IHttpContextAccessor httpContext) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("public-key")]
    public IActionResult GetPublicKey() =>
        Ok(ApiResponse<object>.Ok(new { publicKey = rsa.GetPublicKeyPem() }));

    [AllowAnonymous]
    [HttpGet("company-databases")]
    public IActionResult GetCompanyDatabases() =>
        Ok(ApiResponse<object>.Ok(Enum.GetNames(typeof(Shared.Enums.SapCompanyDatabase))));

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
    [HttpPost("switch-company")]
    public async Task<IActionResult> SwitchCompany([FromBody] SwitchCompanyRequest request, CancellationToken cancellationToken)
    {
        var userId = httpContext.GetUserIdAsync()
            ?? throw new UnauthorizedAccessException();

        var result = await authService.SwitchCompanyAsync(request, userId, cancellationToken);
        return result.Success ? Ok(result) : Unauthorized(result);
    }

    [Authorize]
    [HttpGet("branches")]
    public async Task<IActionResult> GetBranches(CancellationToken cancellationToken)
    {
        var branches = await masterDataService.ListBranchOptionsAsync(cancellationToken);
        return Ok(ApiResponse<object>.Ok(branches));
    }

    [Authorize]
    [HttpPost("switch-branch")]
    public async Task<IActionResult> SwitchBranch([FromBody] SwitchBranchRequest request, CancellationToken cancellationToken)
    {
        var userId = httpContext.GetUserIdAsync()
            ?? throw new UnauthorizedAccessException();

        var result = await authService.SwitchBranchAsync(request, userId, cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var result = await authService.LogoutAsync(cancellationToken);
        return Ok(result);
    }
}
