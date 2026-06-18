namespace SapApi.Domain.Models;

public class SapRenewalCredentials
{
    public required string UserName { get; init; }
    public required string EncryptedPassword { get; init; }
}
