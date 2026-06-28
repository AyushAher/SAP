using SapApi.Services.Helpers;
using SapApi.Modals;
using SapApi.Modals.Responses.Sap;

namespace SapApi.Services
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
