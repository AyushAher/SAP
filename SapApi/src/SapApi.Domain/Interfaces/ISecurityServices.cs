namespace SapApi.Domain.Interfaces;

public interface IAesEncryptionService
{
    string Encrypt(string plainText);
    string Decrypt(string cipherText);
}

public interface IRsaDecryptionService
{
    string Decrypt(string cipherTextBase64);
    string GetPublicKeyPem();
}

public interface IHmacVerificationService
{
    string ComputeSignature(string payload);
    bool VerifySignature(string payload, string signature);
}
