using SapApi.Domain.Interfaces;
using SapApi.Shared;
using SapApi.Shared.Responses.Sap;

namespace SapApi.Infrastructure.Services.Sap
{
    public class SapTaxCodesService(IHttpRequestHandler requestHandler)
    {
        public Task<GetAllSapTaxCodesResponse?> GetAllTaxCodes()
        {
            return requestHandler.GetAsync<GetAllSapTaxCodesResponse>(
                Constants.SapApiUrls.GetAllSalesTaxCodes);
        }
    }
}
