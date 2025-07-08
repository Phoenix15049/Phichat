using System.Security.Cryptography;
using System.Text;
using Phichat.Application.Interfaces;

public class EncryptionService : IEncryptionService
{
    public string EncryptWithPublicKey(string publicKeyPem, string plainText)
    {
        using var rsa = RSA.Create();
        rsa.ImportFromPem(publicKeyPem.ToCharArray());

        var bytes = Encoding.UTF8.GetBytes(plainText);
        var encrypted = rsa.Encrypt(bytes, RSAEncryptionPadding.Pkcs1);

        return Convert.ToBase64String(encrypted);
    }
}
