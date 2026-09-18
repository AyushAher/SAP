using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using SapApi.Domain.Interfaces;
using SapApi.Infrastructure.Services;
using SapApi.Shared.Configuration;

namespace SapApi.Tests.Services.Mail;

[TestFixture]
public class GraphMailSenderTests
{
    private const string SenderMailbox = "no-reply@example.onmicrosoft.com";

    private static GraphMailOptions Configured() => new()
    {
        TenantId = "tenant-1",
        ClientId = "client-1",
        ClientSecret = "secret-1",
        SenderMailbox = SenderMailbox,
    };

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return respond(request);
        }
    }

    private static (GraphMailSender Sender, FakeHandler Handler, Mock<IGraphAccessTokenProvider> Token) BuildSut(
        GraphMailOptions? options = null,
        HttpStatusCode statusCode = HttpStatusCode.Accepted)
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(statusCode == HttpStatusCode.Accepted ? string.Empty : "{\"error\":\"boom\"}"),
        });
        var http = new HttpClient(handler);
        var tokenProvider = new Mock<IGraphAccessTokenProvider>();
        tokenProvider.Setup(t => t.GetAccessTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync("fake-token");
        var sender = new GraphMailSender(http, tokenProvider.Object, Options.Create(options ?? Configured()));
        return (sender, handler, tokenProvider);
    }

    [Test]
    public async Task Sends_to_the_configured_mailboxs_sendMail_endpoint_with_a_bearer_token()
    {
        var (sender, handler, _) = BuildSut();

        await sender.SendAsync(new MailMessage
        {
            Subject = "Hello",
            Body = "<p>Hi</p>",
            IsHtml = true,
            To = ["vendor@example.com"],
        });

        handler.LastRequest.Should().NotBeNull();
        handler.LastRequest!.RequestUri!.ToString().Should().Be(
            $"https://graph.microsoft.com/v1.0/users/{Uri.EscapeDataString(SenderMailbox)}/sendMail");
        handler.LastRequest.Headers.Authorization!.Scheme.Should().Be("Bearer");
        handler.LastRequest.Headers.Authorization.Parameter.Should().Be("fake-token");
    }

    [Test]
    public async Task Payload_carries_subject_html_body_and_recipients()
    {
        var (sender, handler, _) = BuildSut();

        await sender.SendAsync(new MailMessage
        {
            Subject = "Payment Advice - PO 123",
            Body = "<p>Details</p>",
            IsHtml = true,
            To = ["vendor@example.com", "vendor2@example.com"],
            Cc = ["accounts@example.com"],
        });

        using var doc = JsonDocument.Parse(handler.LastBody!);
        var message = doc.RootElement.GetProperty("message");
        message.GetProperty("subject").GetString().Should().Be("Payment Advice - PO 123");
        message.GetProperty("body").GetProperty("contentType").GetString().Should().Be("HTML");
        message.GetProperty("body").GetProperty("content").GetString().Should().Be("<p>Details</p>");

        var to = message.GetProperty("toRecipients").EnumerateArray()
            .Select(r => r.GetProperty("emailAddress").GetProperty("address").GetString())
            .ToList();
        to.Should().BeEquivalentTo(["vendor@example.com", "vendor2@example.com"]);

        var cc = message.GetProperty("ccRecipients").EnumerateArray()
            .Select(r => r.GetProperty("emailAddress").GetProperty("address").GetString())
            .ToList();
        cc.Should().BeEquivalentTo(["accounts@example.com"]);

        doc.RootElement.GetProperty("saveToSentItems").GetBoolean().Should().BeFalse();
    }

    [Test]
    public void Throws_when_graph_returns_a_non_success_status()
    {
        var (sender, _, _) = BuildSut(statusCode: HttpStatusCode.Forbidden);

        var act = () => sender.SendAsync(new MailMessage
        {
            Subject = "Hello",
            Body = "Hi",
            To = ["vendor@example.com"],
        });

        act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Forbidden*");
    }

    [TestCase("", "client-1", "secret-1", SenderMailbox)]
    [TestCase("tenant-1", "", "secret-1", SenderMailbox)]
    [TestCase("tenant-1", "client-1", "", SenderMailbox)]
    [TestCase("tenant-1", "client-1", "secret-1", "")]
    public void Throws_when_graph_mail_is_not_fully_configured(
        string tenantId, string clientId, string clientSecret, string senderMailbox)
    {
        var (sender, _, _) = BuildSut(new GraphMailOptions
        {
            TenantId = tenantId,
            ClientId = clientId,
            ClientSecret = clientSecret,
            SenderMailbox = senderMailbox,
        });

        var act = () => sender.SendAsync(new MailMessage
        {
            Subject = "Hello",
            Body = "Hi",
            To = ["vendor@example.com"],
        });

        act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not configured*");
    }
}
