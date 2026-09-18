using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SapApi.Domain.Interfaces;
using SapApi.Shared.Models;

namespace SapApi.Api.Controllers;

[ApiController]
[Route("api/email")]
public class EmailController(IMailSender mailSender) : ControllerBase
{
    public const string TestRecipient = "ayushaher118@gmail.com";

    [AllowAnonymous]
    [HttpPost("test")]
    public async Task<IActionResult> SendTest(CancellationToken cancellationToken)
    {
        await mailSender.SendAsync(new MailMessage
        {
            Subject = "SAP Portal test email",
            Body = "<p>This is a test email from the SAP Portal API.</p>",
            IsHtml = true,
            To = [TestRecipient],
        }, cancellationToken);

        return Ok(ApiResponse<object>.Ok(new { to = TestRecipient }, "Test email sent"));
    }
}
