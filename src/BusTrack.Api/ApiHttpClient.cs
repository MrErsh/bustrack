using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace BusTrack.Api;

/// <summary>
/// HttpClient для портала транспорта СПб. Сервер не отдаёт промежуточный сертификат,
/// а цепочка ведёт к Russian Trusted Root CA, которого нет в системных хранилищах Android.
/// Корень и промежуточный сертификат вшиты в приложение; доверяем только цепочкам,
/// заканчивающимся именно этим корнем (проверка имени хоста сохраняется).
/// </summary>
public static class ApiHttpClient
{
    private static readonly X509Certificate2 Root = Load("russian_trusted_root.der");
    private static readonly X509Certificate2 Sub = Load("russian_trusted_sub.der");

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

    private static bool Validate(
        HttpRequestMessage request,
        X509Certificate2? certificate,
        X509Chain? chain,
        SslPolicyErrors sslPolicyErrors)
    {
        if (certificate is null)
            return false;
        if (sslPolicyErrors.HasFlag(SslPolicyErrors.RemoteCertificateNameMismatch))
            return false;

        using var custom = new X509Chain();
        custom.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        custom.ChainPolicy.ExtraStore.Add(Sub);
        custom.ChainPolicy.ExtraStore.Add(Root);

        var built = custom.Build(certificate);
        var rootIsPinned = custom.ChainElements.Count > 0
            && custom.ChainElements[^1].Certificate.Thumbprint == Root.Thumbprint;
        if (!rootIsPinned)
            return false;

        // Единственная допустимая ошибка — корень не в системном хранилище.
        return built || custom.ChainStatus.All(s => s.Status == X509ChainStatusFlags.UntrustedRoot);
    }
}