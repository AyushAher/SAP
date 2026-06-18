using Microsoft.AspNetCore.Mvc;
using SapApi.Modals.Responses.Sap;
using SapApi.Services;
using SapApi.Services.Login;

namespace SapApi.Controllers
{
    [ApiController]
    [Route("/api/business-partner")]
    public class BusinessPartnerController(BusinessPartnerService businessPartnerService, LoginService loginService) : ControllerBase
    {
        [HttpGet]
        public async Task<SapBusinessPartnerResponse?> Get()
        {
            await loginService.SapLogin();
            return await businessPartnerService.GetAllBusinessPartners();
        }
    }
}