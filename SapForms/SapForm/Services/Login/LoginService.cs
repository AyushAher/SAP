using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using SapForm.Services.Helpers;
using Shared;
using Shared.Configuration;
using Shared.Requests;
using Shared.Responses.Sap;
using System.Text;
using System.Text.Json;

namespace SapForm.Services.Login
{
    public class LoginService(RedisCacheService redisCache, IOptions<SapCredentials> sapCredentialsOptions)
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
            var decodedPasswordBytes = Convert.FromBase64String(Environment.GetEnvironmentVariable("SAP_PASSWORD") ??
                                                                throw new ApiErrorException(
                                                                    BaseErrorCodes.IncorrectCredentials,
                                                                    "SAP Credentials Incorrect"));

            var client = new HttpClient(new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            });

            var request = new HttpRequestMessage(HttpMethod.Post, Constants.SapApiUrls.Login)
            {
                Content = new StringContent(JsonSerializer.Serialize(new SapLoginRequest
                {
                    CompanyDB = sapCredentialsOptions.Value.CompanyDb ??
                                    throw new ApiErrorException(BaseErrorCodes.NullValue),
                    Password = Encoding.UTF8.GetString(decodedPasswordBytes),
                    UserName = sapCredentialsOptions.Value.Username ??
                                   throw new ApiErrorException(BaseErrorCodes.NullValue)
                }), Encoding.UTF8, "application/json")
            };

            HttpResponseMessage response = await client.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                Serilog.Log.Information("Logged in successfully");
                SapLoginResponse? sapResponse = await response.Content.ReadFromJsonAsync<SapLoginResponse>();

                if (string.IsNullOrEmpty(sapResponse?.SessionId))
                {
                    throw new ApiErrorException(sapResponse?.Error?.Message?.Value ?? "Some Error Occurred");
                }

                await redisCache.SetAsync("sessionId", sapResponse.SessionId, TimeSpan.FromMinutes(sapResponse.SessionTimeout ?? 30));
            }
        }
    }
}