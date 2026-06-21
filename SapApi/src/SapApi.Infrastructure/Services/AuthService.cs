using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using RotatingJwt;
using SapApi.Domain.Entities;
using SapApi.Domain.Interfaces;
using SapApi.Shared;
using SapApi.Shared.Configuration;
using SapApi.Shared.Enums;
using SapApi.Shared.Models;
using SapApi.Shared.Exceptions;
using SapApi.Shared.Requests.Account;
using SapApi.Shared.Responses.Account;

namespace SapApi.Infrastructure.Services;

public class AuthService(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    IRsaDecryptionService rsa,
    ISapLoginService sapLoginService,
    IJwtTokenService jwtTokenService,
    IOptions<ApplicationConfiguration> appConfig)
{
    public async Task<ApiResponse<LoginResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        string password;
        try
        {
            password = DecryptIfNeeded(request.Password);
        }
        catch (ApiErrorException ex)
        {
            return ApiResponse<LoginResponse>.Fail(ex.ErrorCode, ex.Message);
        }
        var user = await userManager.FindByNameAsync(request.UserName);
        if (user == null)
            return ApiResponse<LoginResponse>.Fail(BaseErrorCodes.IncorrectCredentials, "Invalid credentials");

        var result = await signInManager.CheckPasswordSignInAsync(user, password, false);
        if (!result.Succeeded)
            return ApiResponse<LoginResponse>.Fail(BaseErrorCodes.IncorrectCredentials, "Invalid credentials");

        if (!appConfig.Value.SkipSapLoginOnUserAuth)
        {
            try
            {
                await sapLoginService.LoginWithUserCredentialsAsync(user.Id, request.UserName, password, request.CompanyDb, cancellationToken);
            }
            catch (ApiErrorException ex)
            {
                return ApiResponse<LoginResponse>.Fail(ex.ErrorCode, ex.Message);
            }
        }

        var roles = await userManager.GetRolesAsync(user);
        var claims = BuildUserClaims(user, roles, request.CompanyDb);
        var tokenResponse = await jwtTokenService.GenerateAccessToken(user.Id.ToString(), claims, needRefreshToken: true);

        return ApiResponse<LoginResponse>.Ok(BuildLoginResponse(tokenResponse, user, roles, request.CompanyDb));
    }

    public async Task<ApiResponse<LoginResponse>> SwitchCompanyAsync(SwitchCompanyRequest request, int userId, string userName, CancellationToken cancellationToken = default)
    {
        string password;
        try
        {
            password = DecryptIfNeeded(request.Password);
        }
        catch (ApiErrorException ex)
        {
            return ApiResponse<LoginResponse>.Fail(ex.ErrorCode, ex.Message);
        }

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
            return ApiResponse<LoginResponse>.Fail(BaseErrorCodes.IncorrectCredentials, "Invalid credentials");

        var passwordValid = await userManager.CheckPasswordAsync(user, password);
        if (!passwordValid)
            return ApiResponse<LoginResponse>.Fail(BaseErrorCodes.IncorrectCredentials, "Invalid credentials");

        await sapLoginService.LogoutAsync(cancellationToken);

        if (!appConfig.Value.SkipSapLoginOnUserAuth)
        {
            try
            {
                await sapLoginService.LoginWithUserCredentialsAsync(user.Id, userName, password, request.CompanyDb, cancellationToken);
            }
            catch (ApiErrorException ex)
            {
                return ApiResponse<LoginResponse>.Fail(ex.ErrorCode, ex.Message);
            }
        }

        var roles = await userManager.GetRolesAsync(user);
        var claims = BuildUserClaims(user, roles, request.CompanyDb);
        var tokenResponse = await jwtTokenService.GenerateAccessToken(user.Id.ToString(), claims, needRefreshToken: true);

        return ApiResponse<LoginResponse>.Ok(BuildLoginResponse(tokenResponse, user, roles, request.CompanyDb));
    }

    public async Task<ApiResponse<LoginResponse>> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return ApiResponse<LoginResponse>.Fail(BaseErrorCodes.IncorrectCredentials, "Refresh token is required");

        if (request.CompanyDb is null)
            return ApiResponse<LoginResponse>.Fail(BaseErrorCodes.NullValue, "Company database is required for token refresh");

        var userId = await jwtTokenService.ResolveUserIdFromRefreshTokenAsync(request.RefreshToken);
        if (userId is null)
            return ApiResponse<LoginResponse>.Fail(BaseErrorCodes.IncorrectCredentials, "Invalid refresh token");

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
            return ApiResponse<LoginResponse>.Fail(BaseErrorCodes.IncorrectCredentials, "Invalid refresh token");

        var roles = await userManager.GetRolesAsync(user);
        var claims = BuildUserClaims(user, roles, request.CompanyDb.Value);

        try
        {
            var tokenResponse = await jwtTokenService.GenerateTokenByRefreshToken(request.RefreshToken, claims);
            return ApiResponse<LoginResponse>.Ok(BuildLoginResponse(tokenResponse, user, roles, request.CompanyDb.Value));
        }
        catch (SecurityTokenException ex)
        {
            return ApiResponse<LoginResponse>.Fail(BaseErrorCodes.IncorrectCredentials, ex.Message);
        }
    }

    public async Task<ApiResponse<object>> LogoutAsync(CancellationToken cancellationToken = default)
    {
        await sapLoginService.LogoutAsync(cancellationToken);
        return ApiResponse<object>.Ok(null, "Logged out");
    }

    public async Task<ApiResponse<RegisterUserResponse>> RegisterAsync(RegisterUserRequest request, CancellationToken cancellationToken = default)
    {
        string password;
        try
        {
            password = DecryptIfNeeded(request.Password);
        }
        catch (ApiErrorException ex)
        {
            return ApiResponse<RegisterUserResponse>.Fail(ex.ErrorCode, ex.Message);
        }

        if (!appConfig.Value.SkipSapLoginOnUserAuth)
        {
            try
            {
                await sapLoginService.ValidateCredentialsAsync(request.UserName!, password, request.CompanyDb, cancellationToken);
            }
            catch (ApiErrorException ex)
            {
                return ApiResponse<RegisterUserResponse>.Fail(ex.ErrorCode, ex.Message);
            }
        }

        var user = new ApplicationUser
        {
            UserName = request.UserName,
            Email = request.Email,
            FullName = request.FullName
        };

        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
            return ApiResponse<RegisterUserResponse>.Fail("SYS-03", string.Join(", ", result.Errors.Select(e => e.Description)));

        await userManager.AddToRoleAsync(user, Constants.Roles.Standard);
        return ApiResponse<RegisterUserResponse>.Ok(new RegisterUserResponse { Succeeded = true });
    }

    private string DecryptIfNeeded(string value)
    {
        var looksEncrypted = value.Length > 100 && !value.Contains(' ');
        if (!looksEncrypted)
            return value;

        try
        {
            return rsa.Decrypt(value);
        }
        catch
        {
            throw new ApiErrorException(
                BaseErrorCodes.SystemError,
                "Unable to decrypt credentials. The server encryption key is not configured correctly.");
        }
    }

    private static List<Claim> BuildUserClaims(ApplicationUser user, IList<string> roles, SapCompanyDatabase companyDb)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName ?? string.Empty),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new("FullName", user.FullName ?? string.Empty),
            new(SapClaimTypes.CompanyDb, companyDb.ToString())
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));
        return claims;
    }

    private static LoginResponse BuildLoginResponse(TokenResponse tokenResponse, ApplicationUser user, IList<string> roles, SapCompanyDatabase companyDb)
    {
        var claims = new List<ClaimsDto>
        {
            new() { Type = ClaimTypes.NameIdentifier, Value = user.Id.ToString() },
            new() { Type = ClaimTypes.Email, Value = user.Email ?? string.Empty },
            new() { Type = "FullName", Value = user.FullName ?? string.Empty },
            new() { Type = SapClaimTypes.CompanyDb, Value = companyDb.ToString() }
        };
        claims.AddRange(roles.Select(r => new ClaimsDto { Type = ClaimTypes.Role, Value = r }));

        return new LoginResponse
        {
            Token = tokenResponse.Token,
            RefreshToken = tokenResponse.RefreshToken,
            Claims = claims
        };
    }
}
