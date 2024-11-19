using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.UserSecrets;

namespace AppHost;

public static class SslCertificateManager
{
    private static string UserSecretsId => Assembly.GetEntryAssembly()!.GetCustomAttribute<UserSecretsIdAttribute>()!.UserSecretsId;

    public static string CreateSslCertificateIfNotExists(this IDistributedApplicationBuilder builder)
    {
        var config = new ConfigurationBuilder().AddUserSecrets(UserSecretsId).Build();

        const string certificatePasswordKey = "certificate-password";
        var certificatePassword = config[certificatePasswordKey]
                                  ?? builder.CreateStablePassword(certificatePasswordKey).Resource.Value;

        var certificateLocation = GetCertificateLocation("localhost");
        try
        {
            X509CertificateLoader.LoadPkcs12FromFile(certificateLocation, certificatePassword);
        }
        catch (CryptographicException)
        {
            CreateNewSelfSignedDeveloperCertificate(certificateLocation, certificatePassword);
        }

        return certificatePassword;
    }

    private static string GetCertificateLocation(string domain)
    {
        var userFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return $"{userFolder}/.aspnet/dev-certs/https/{domain}.pfx";
    }

    private static void CreateNewSelfSignedDeveloperCertificate(string certificateLocation, string password)
    {
        if (File.Exists(certificateLocation))
        {
            Console.WriteLine($"Certificate {certificateLocation} exists, but password {password} was invalid. Creating a new certificate.");

            File.Delete(certificateLocation);
        }

        Process.Start(new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"dev-certs https --trust -ep {certificateLocation} -p {password}",
                RedirectStandardOutput = false,
                RedirectStandardError = false,
                UseShellExecute = false
            }
        )!.WaitForExit();
    }
}
