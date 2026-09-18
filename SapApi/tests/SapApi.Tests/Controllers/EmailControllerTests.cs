using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SapApi.Api.Controllers;
using SapApi.Domain.Interfaces;
using SapApi.Shared.Models;

namespace SapApi.Tests.Controllers;

[TestFixture]
public class EmailControllerTests
{
    [Test]
    public async Task SendTest_sends_to_configured_recipient()
    {
        MailMessage? captured = null;
        var mailSender = new Mock<IMailSender>();
        mailSender
            .Setup(s => s.SendAsync(It.IsAny<MailMessage>(), It.IsAny<CancellationToken>()))
            .Callback<MailMessage, CancellationToken>((message, _) => captured = message)
            .Returns(Task.CompletedTask);

        var sut = new EmailController(mailSender.Object);
        var result = await sut.SendTest(CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var body = ok.Value.Should().BeAssignableTo<ApiResponse<object>>().Subject;
        body.Success.Should().BeTrue();
        body.Message.Should().Be("Test email sent");

        captured.Should().NotBeNull();
        captured!.To.Should().ContainSingle(a => a == EmailController.TestRecipient);
        captured.Subject.Should().Be("SAP Portal test email");
        mailSender.Verify(s => s.SendAsync(It.IsAny<MailMessage>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
