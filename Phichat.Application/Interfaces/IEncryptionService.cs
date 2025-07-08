public interface IEncryptionService
{
    string EncryptWithPublicKey(string publicKey, string plainText);
}
