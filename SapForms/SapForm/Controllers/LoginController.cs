using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Shared.Entities;
using Shared.Requests.Account;
using SapForm.Services.Login;
using SapForm.Services.Helpers;

namespace SapForm.Controllers
{
    [ApiController]
    [Route("/api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly LoginService _loginService;
        private readonly PdfService _pdfService;

        public AuthController(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager, LoginService loginService, PdfService pdfService)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _loginService = loginService;
            _pdfService = pdfService;
        }


        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            ApplicationUser? user = await _userManager.FindByNameAsync(request.UserName);

            if (user == null)
                return Unauthorized();

            Microsoft.AspNetCore.Identity.SignInResult result = await _signInManager.PasswordSignInAsync(
                user,
                request.Password,
                true,
                false);

            if (!result.Succeeded)
                return Unauthorized();
            await _loginService.SapLogin();
            return Ok();
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return Ok();
        }

        [HttpGet("get-pdf")]
        public async Task<IActionResult> GetPdf()
        {
            var html = await System.IO.File.ReadAllTextAsync(Path.Join(Directory.GetCurrentDirectory(), "wwwroot", "issue-for-production-template.html"));
            var pdfBytes = _pdfService.GeneratePdfFromHtml(html);
            Response.Headers.Add("Content-Disposition", $"attachment; filename=template.pdf");
            return File(pdfBytes, "application/octet-stream", "template.pdf");
        }
    }
}