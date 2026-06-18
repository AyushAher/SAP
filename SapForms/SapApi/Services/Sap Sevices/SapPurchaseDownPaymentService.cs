using SapApi.Services.Helpers;
using SapApi.Modals;
using SapApi.Modals.Requests;
using SapApi.Modals.Responses.Sap;

namespace SapApi.Services
{
    public class SapPurchaseDownPaymentService(IHttpRequestHandler httpRequestHandler)
    {
        public Task<SapPurchaseDownPaymentResponse?> SaveDownPayment(SapPurchaseDownPaymentRequest request)
        {
            return httpRequestHandler.PostAsync<SapPurchaseDownPaymentRequest, SapPurchaseDownPaymentResponse>(
                Constants.SapApiUrls.PurchaseDownPayment, request);
        }
    }
}