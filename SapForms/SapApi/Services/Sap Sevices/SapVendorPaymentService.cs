using SapApi.Services.Helpers;
using SapApi.Modals;
using SapApi.Modals.Requests;
using SapApi.Modals.Responses.Sap;

namespace SapApi.Services
{
    public class SapVendorPaymentService(IHttpRequestHandler httpRequestHandler)
    {
        public async Task<SapVendorPaymentsResponse?> CreateVendorPayments(SapVendorPaymentRequests requests)
        {
            SapVendorPaymentsResponse? data = await httpRequestHandler.PostAsync<SapVendorPaymentRequests, SapVendorPaymentsResponse>(
                Constants.SapApiUrls.CreateVendorPayments,
                requests);
            return data;
        }
    }
}