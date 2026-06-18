using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using SapApi.Services.Helpers;
using SapApi.Modals;
using SapApi.Modals.Configuration;
using SapApi.Modals.Requests;
using SapApi.Modals.Requests.Account;
using SapApi.Modals.Responses.Account;
using SapApi.Modals.Responses.Sap;
using System.Security.Claims;
using System.Text;

namespace SapApi.Services.Login
{
    public class LoginService(
        IHttpRequestHandler requestHandler,
        UserSession session,
        IMemoryCache cache,
        IOptions<SapCredentials> sapCredentialsOptions)
    {
        /// <summary>
        /// Authenticates a user with the SAP system using the provided login request and establishes a session for
        /// subsequent requests.
        /// </summary>
        /// <remarks>Upon successful authentication, a claims-based identity is created and signed in
        /// using cookie authentication. This method must be called in the context of an active HTTP request.</remarks>
        /// <returns>A task that represents the asynchronous login operation.</returns>
        /// <exception cref="ApiErrorException">Thrown when the SAP login fails or the response does not contain a valid session identifier.</exception>
        public async Task SapLogin()
        {
            var checkExistingClaims = CheckExistingClaims();
            if (checkExistingClaims)
            {
                return;
            }

            var decodedPasswordBytes = Convert.FromBase64String(Environment.GetEnvironmentVariable("SAP_PASSWORD") ??
                                                                throw new ApiErrorException(
                                                                    BaseErrorCodes.IncorrectCredentials,
                                                                    "SAP Credentials Incorrect"));
            SapLoginResponse? response =
                await requestHandler.PostAsync<SapLoginRequest, SapLoginResponse>(Constants.SapApiUrls.Login,
                    new SapLoginRequest
                    {
                        CompanyDB = sapCredentialsOptions.Value.CompanyDb ??
                                    throw new ApiErrorException(BaseErrorCodes.NullValue),
                        Password = Encoding.UTF8.GetString(decodedPasswordBytes),
                        UserName = sapCredentialsOptions.Value.Username ??
                                   throw new ApiErrorException(BaseErrorCodes.NullValue)
                    });

            if (string.IsNullOrEmpty(response?.SessionId))
            {
                throw new ApiErrorException(response?.Error?.Message?.Value ?? "Some Error Occurred");
            }

            cache.Set("sessionId", response.SessionId, TimeSpan.FromMinutes(response.SessionTimeout ?? 30));
        }

        public bool CheckExistingClaims()
            => !string.IsNullOrEmpty(cache.Get<string>("sessionId"));

        public async Task<LoginResponse> UserLogin(LoginRequest loginRequest)
        {
            LoginResponse? response =
                await requestHandler.PostAsync<LoginRequest, LoginResponse>(
                    Constants.AuthServiceApiUrls.LoginUrl(loginRequest),
                    loginRequest);
            if (response is null || string.IsNullOrEmpty(response.Token))
            {
                throw new ApiErrorException(response?.ErrorCode ?? BaseErrorCodes.SystemError,
                    response?.ErrorDescription ?? "Unknown error occurred while login");
            }

            var claims = response.Claims.Select(x => new Claim(x.Type, x.Value)).ToList();
            claims.Add(new Claim("token", response.Token));
            claims.Add(new Claim("refreshToken", response.RefreshToken));

            var identity = new ClaimsIdentity(claims, "MyCookieAuth");
            var principal = new ClaimsPrincipal(identity);
            session.SignIn(principal);
            return response;
        }
    }
}