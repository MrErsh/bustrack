using System.Formats.Asn1;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace BusTrack.Api;

/// <summary>
/// HttpClient для портала транспорта СПб. Цепочка сервера ведёт к Russian Trusted Root CA,
/// которого нет в системных хранилищах Android/Windows. Корень и промежуточный сертификат
/// вшиты в приложение.
///
/// Проверка НЕ использует X509Chain: на Android он не строит цепочки для недоверенных
/// сертификатов (ChainElements пуст, ExtraStore игнорируется — dotnet/runtime#84202).
/// Вместо этого подпись листового сертификата проверяется напрямую ключом вшитого
/// Sub CA, плюс срок действия и имя хоста. Доверие ограничено вшитым Sub CA
/// (его подпись корнем проверяется один раз при старте).
/// </summary>
public static class ApiHttpClient
{
    private static readonly X509Certificate2 Root = Load("russian_trusted_root.der");
    private static readonly X509Certificate2 Sub = Load("russian_trusted_sub.der");

    static ApiHttpClient()
    {
        if (!IsSignedBy(Sub, Root))
            throw new InvalidOperationException("Embedded Sub CA is not signed by the pinned root");
    }

    public static HttpClient Create()
    {
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = Validate
        };
        return new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
    }

    private static X509Certificate2 Load(string name)
    {
        using var stream = typeof(ApiHttpClient).Assembly
            .GetManifestResourceStream($"BusTrack.Api.Resources.{name}")
            ?? throw new InvalidOperationException($"Embedded resource {name} not found");
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return X509CertificateLoader.LoadCertificate(ms.ToArray());
    }

    internal static bool Validate(
        HttpRequestMessage? request,
        X509Certificate2? certificate,
        X509Chain? chain,
        SslPolicyErrors sslPolicyErrors)
    {
        if (certificate is null)
            return false;
        if (sslPolicyErrors.HasFlag(SslPolicyErrors.RemoteCertificateNameMismatch))
            return false;
        if (sslPolicyErrors == SslPolicyErrors.None)
            return true;

        var now = DateTime.Now;
        if (now < certificate.NotBefore || now > certificate.NotAfter)
            return false;
        if (!string.Equals(certificate.Issuer, Sub.Subject, StringComparison.Ordinal))
            return false;

        return IsSignedBy(certificate, Sub);
    }

    /// <summary>
    /// Проверяет подпись сертификата открытым ключом издателя (RSA PKCS#1 v1.5).
    /// Certificate ::= SEQUENCE { tbsCertificate, signatureAlgorithm, signatureValue };
    /// подпись считается от DER-кодированного tbsCertificate целиком.
    /// </summary>
    internal static bool IsSignedBy(X509Certificate2 certificate, X509Certificate2 issuer)
    {
        var seq = new AsnReader(certificate.RawData, AsnEncodingRules.DER).ReadSequence();
        var tbs = seq.ReadEncodedValue();
        var algorithm = seq.ReadSequence();
        var oid = algorithm.ReadObjectIdentifier();
        var signature = seq.ReadBitString(out _);

        var hash = oid switch
        {
            "1.2.840.113549.1.1.11" => HashAlgorithmName.SHA256,
            "1.2.840.113549.1.1.12" => HashAlgorithmName.SHA384,
            "1.2.840.113549.1.1.13" => HashAlgorithmName.SHA512,
            _ => (HashAlgorithmName?)null
        };
        if (hash is null)
            return false;

        using var rsa = issuer.GetRSAPublicKey();
        return rsa is not null
            && rsa.VerifyData(tbs.Span, signature, hash.Value, RSASignaturePadding.Pkcs1);
    }
}