
using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography.X509Certificates;
using TheBentern.Tak.Client.Generated;

namespace TheBentern.Tak.Client.Providers;

internal class CertificateCollectionProvider
{
    private const string PrefsKey = "com.atakmap.app_preferences";
    private const string CertLocationKey = "certificateLocation";
    private const string CertPasswordKey = "clientPassword";

    private readonly string packagePath;

    public CertificateCollectionProvider(string packagePath)
    {
        this.packagePath = packagePath;
    }

    public X509CertificateCollection GetCollection(Preferences manifest)
    {
        try
        {
            var collection =new X509CertificateCollection();

            using var package = new ZipArchive(new FileStream(packagePath, FileMode.Open));
            var pref = manifest.Preference.First(p => p.Name == PrefsKey);

            //var caFileName = Path.GetFileName(pref.Entry.First(e => e.Key == "caLocation").Text);
            //var caPassword = pref.Entry.FirstOrDefault(e => e.Key == "caPassword")?.Text;
            //using var caStream = new MemoryStream();
            //var caEntry = package.Entries.First(e => e.Name.Contains(caFileName));
            //caEntry.Open().CopyToAsync(caStream);
            //var ca = new X509Certificate(caStream.ToArray(), caPassword);
            //collection.Add(ca);

            var certFileName = Path.GetFileName(pref.Entry.First(e => e.Key == CertLocationKey).Text);
            var certPassword = pref.Entry.FirstOrDefault(e => e.Key == CertPasswordKey)?.Text;
            using var certStream = new MemoryStream();
            var certEntry = package.Entries.First(e => e.Name.Contains(certFileName));
            certEntry.Open().CopyToAsync(certStream);

        
            var cert = new X509Certificate(certStream.ToArray(), certPassword);
            collection.Add(cert);
            return collection;
        }
        catch (Exception ex)
        {
            while (ex.InnerException != null) ex = ex.InnerException;
            Debug.WriteLine(ex.Message);
            throw;
        }
    }
}
