namespace SapApi.Domain.Interfaces;

public sealed record MailMessage
{
    public required string Subject { get; init; }
    public required string Body { get; init; }
    public bool IsHtml { get; init; } = true;
    public required IReadOnlyList<string> To { get; init; }
    public IReadOnlyList<string> Cc { get; init; } = [];
}

public interface IMailSender
{
    Task SendAsync(MailMessage message, CancellationToken cancellationToken = default);
}
