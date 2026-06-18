using SapApi.Domain.Interfaces;
using SapApi.Shared;
using SapApi.Shared.Requests;
using SapApi.Shared.Responses.Sap;

namespace SapApi.Infrastructure.Services.Sap
{
    public class BusinessPartnerService(IHttpRequestHandler requestHandler)
    {
        public async Task<SapBusinessPartnerResponse?> GetAllBusinessPartners()
        {
            return await requestHandler.GetAsync<SapBusinessPartnerResponse>(Constants.SapApiUrls.GetAllBusinessPartners);
        }
        public async Task<SapBusinessPartnerResponse?> GetAllCustomers()
        {
            return await requestHandler.GetAsync<SapBusinessPartnerResponse>(Constants.SapApiUrls.GetAllCustomers);
        }

        public async Task<SapBusinessPartnerResponse?> SaveBusinessPartners(SapBusinessPartnerRequest request)
        {
            return await requestHandler.PostAsync<SapBusinessPartnerRequest, SapBusinessPartnerResponse>(
                Constants.SapApiUrls.SaveBusinessPartners, request);
        }
        public async Task<GetAllWithholdingTaxDataCollectionResponse?> GetAllWithholdingTaxDataCollectionResponse()
        {
            return await requestHandler.GetAsync<GetAllWithholdingTaxDataCollectionResponse>(
                Constants.SapApiUrls.GetAllWithholdingTaxDataCollection);
        }

    }
}
