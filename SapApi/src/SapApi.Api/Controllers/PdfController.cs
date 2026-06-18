using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SapApi.Domain.Interfaces;
using SapApi.Shared.Models;

namespace SapApi.Api.Controllers;

[ApiController]
[Route("api/pdf")]
[Authorize]
public class PdfController(IPdfService pdfService) : ControllerBase
{
    private static readonly HashSet<string> AllowedTemplates =
    [
        "issue-for-production-template.html",
        "production-order-template.html",
        "receipt-from-production-template.html",
        "outgoing-payment-template.html"
    ];

    [HttpPost("generate")]
    public async Task<IActionResult> Generate([FromBody] PdfGenerateRequest request, CancellationToken cancellationToken)
    {
        if (!AllowedTemplates.Contains(request.TemplateName))
            return BadRequest(ApiResponse<object>.Fail("SYS-02", "Invalid template name"));

        var pdfBytes = string.IsNullOrEmpty(request.Html)
            ? await pdfService.GeneratePdfFromTemplateAsync(request.TemplateName, request.Placeholders ?? [], cancellationToken)
            : pdfService.GeneratePdfFromHtml(request.Html);

        return File(pdfBytes, "application/pdf", request.FileName ?? "document.pdf");
    }

    [HttpGet("{templateName}")]
    public async Task<IActionResult> GetTemplatePdf(string templateName, CancellationToken cancellationToken)
    {
        if (!AllowedTemplates.Contains(templateName))
            return BadRequest(ApiResponse<object>.Fail("SYS-02", "Invalid template name"));

        var pdfBytes = await pdfService.GeneratePdfFromTemplateAsync(templateName, new Dictionary<string, string>(), cancellationToken);
        return File(pdfBytes, "application/pdf", $"{Path.GetFileNameWithoutExtension(templateName)}.pdf");
    }
}

public class PdfGenerateRequest
{
    public string TemplateName { get; set; } = string.Empty;
    public string? Html { get; set; }
    public string? FileName { get; set; }
    public Dictionary<string, string>? Placeholders { get; set; }
}
