using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using BusTrack.Api;
using FluentAssertions;
using Xunit;

namespace BusTrack.Tests;

public class ApiHttpClientTests
{
    [Fact]
    public void IsSignedBy_RealServerLeaf_VerifiesWithEmbeddedSubCa()
    {
        // Реальный листовой сертификат transport.orgp.spb.ru (снят 2026-11-17)
        // должен проверяться вшитым Sub CA — это и есть сценарий, падавший на Android.
        using var leaf = LoadTestResource("server_leaf.pem");
        using var sub = LoadApiResource("russian_trusted_sub.der");

        ApiHttpClient.IsSignedBy(leaf, sub).Should().BeTrue();
    }

    [Fact]
    public void IsSignedBy_RealSubCa_VerifiesWithEmbeddedRoot()
    {
        using var sub = LoadApiResource("russian_trusted_sub.der");
        using var root = LoadApiResource("russian_trusted_root.der");

        ApiHttpClient.IsSignedBy(sub, root).Should().BeTrue();
    }

    [Fact]
    public void IsSignedBy_GeneratedChain_VerifiesEachLink()
    {
        using var root = CreateCa("CN=Test Root");
        using var sub = CreateCa("CN=Test Sub", root);
        using var leaf = CreateCa("CN=leaf.example", sub);

        ApiHttpClient.IsSignedBy(leaf, sub).Should().BeTrue();
        ApiHttpClient.IsSignedBy(sub, root).Should().BeTrue();
        ApiHttpClient.IsSignedBy(leaf, root).Should().BeFalse("лист подписан не корнем");
    }

    [Fact]
    public void IsSignedBy_WrongIssuerKey_Rejects()
    {
        using var root = CreateCa("CN=Test Root");
        using var another = CreateCa("CN=Another Root");
        using var leaf = CreateCa("CN=leaf.example", root);

        ApiHttpClient.IsSignedBy(leaf, another).Should().BeFalse();
    }

    [Fact]
    public void Validate_NullCertificate_Rejects()
        => ApiHttpClient.Validate(null, null, null, SslPolicyErrors.None).Should().BeFalse();

    [Fact]
    public void Validate_NameMismatch_Rejects()
    {
        using var leaf = LoadTestResource("server_leaf.pem");

        ApiHttpClient.Validate(null, leaf, null, SslPolicyErrors.RemoteCertificateNameMismatch)
            .Should().BeFalse();
    }

    [Fact]
    public void Validate_NoErrors_Accepts()
    {
        using var leaf = LoadTestResource("server_leaf.pem");

        ApiHttpClient.Validate(null, leaf, null, SslPolicyErrors.None).Should().BeTrue();
    }

    private static X509Certificate2 LoadTestResource(string name)
    {
        using var stream = typeof(ApiHttpClientTests).Assembly
            .GetManifestResourceStream($"BusTrack.Tests.Resources.{name}")!;
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return X509CertificateLoader.LoadCertificate(ms.ToArray());
    }

    private static X509Certificate2 LoadApiResource(string name)
    {
        using var stream = typeof(RouteCatalog).Assembly
            .GetManifestResourceStream($"BusTrack.Api.Resources.{name}")!;
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return X509CertificateLoader.LoadCertificate(ms.ToArray());
    }

    private static X509Certificate2 CreateCa(string subject, X509Certificate2? issuer = null)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, true, 0, true));

        var notBefore = DateTimeOffset.UtcNow.Date.AddDays(-1);
        var notAfter = notBefore.AddDays(30);
        using var cert = issuer is null
            ? request.CreateSelfSigned(notBefore, notAfter)
            : request.Create(issuer, notBefore, notAfter, Guid.NewGuid().ToByteArray()).CopyWithPrivateKey(rsa);

        // PFX-круг отделяет закрытый ключ от dispose'нутого RSA.
        return X509CertificateLoader.LoadPkcs12(cert.Export(X509ContentType.Pfx), password: null);
    }
}