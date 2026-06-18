using SapApi.Services.Helpers;
using SapApi.Modals;
using SapApi.Modals.Requests.Account;
using SapApi.Modals.Responses;

namespace SapApi.Services.Login
{
    public class AccountService(IHttpRequestHandler requestHandler)
    {
        public async Task<BaseAuthServiceResponse?> RegisterAccountAsync(RegisterUserRequest registerUserRequest )
        {
            BaseAuthServiceResponse? response = await requestHandler.PostAsync<RegisterUserRequest, BaseAuthServiceResponse>(
                Constants.AuthServiceApiUrls.RegistrationUrl(registerUserRequest.Password), registerUserRequest);
            
            return response;
        }
    }
}
