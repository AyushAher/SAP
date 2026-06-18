using SapApi.Services.Helpers;
using SapApi.Modals;
using SapApi.Modals.Requests;
using SapApi.Modals.Responses.Sap;

namespace SapApi.Services
{
    public class BusinessPartnerService(IHttpRequestHandler requestHandler)
    {
        public async Task<SapBusinessPartnerResponse?> GetAllBusinessPartners()
        {
            return await requestHandler.GetAsync<SapBusinessPartnerResponse>(Constants.SapApiUrls.GetAllBusinessPartners);
        }

        public async Task<SapBusinessPartnerResponse?> SaveBusinessPartners(SapBusinessPartnerRequest request)
        {
            return await requestHandler.PostAsync<SapBusinessPartnerRequest, SapBusinessPartnerResponse>(
                Constants.SapApiUrls.SaveBusinessPartners, request);
        }

    }
}
