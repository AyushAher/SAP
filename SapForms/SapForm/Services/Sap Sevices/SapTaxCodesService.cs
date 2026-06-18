using SapForm.Services.Helpers;
using Shared;
using Shared.Responses.Sap;

namespace SapForm.Services
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
