﻿using dpp.cot;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using System.Text;
using System.Xml;
using System.IO.Compression;
using System.Runtime.ConstrainedExecution;
using Tak.Client.Generated;
using System.Xml.Serialization;
using System.Linq;

namespace Tak.Client;

public class TakClient
{
    private const string CoTStreamsKey = "cot_streams";
    private const string ConnectionStringKey = "connectString0";

    private const string PrefsKey = "com.atakmap.app_preferences";
    private const string CertLocationKey = "certificateLocation";
    private const string CertPasswordKey = "clientPassword";

    private readonly string packagePath;
    private readonly TcpClient client = new();

    private readonly XmlReaderSettings looseXmlReaderSettings = new() { ConformanceLevel = ConformanceLevel.Fragment };

    /// <summary>
    /// Client for connection to TAK server instance
    /// </summary>
    /// <param name="packagePath">Path to an ATAK data package zip file containing the client certificate and pref file</param>
    public TakClient(string packagePath)
    {
        if (String.IsNullOrWhiteSpace(packagePath))
            throw new ArgumentNullException(nameof(packagePath));

        if (!File.Exists(packagePath))
            throw new ArgumentException("Package file could not be found", nameof(packagePath));

        this.packagePath = packagePath;
    }

    public async Task ConnectAsync()
    {
        var manifest = GetPackageManifest();

        var connection = manifest.Preferences.Preference.First(p => p.Name == CoTStreamsKey).Entry.First(e => e.Key == ConnectionStringKey);
        var connectionParams = connection.Text.Split(':');
        var host = connectionParams.First()!;
        var port = Int32.Parse(connectionParams[1]);

        client.Connect(host, port);

        using SslStream sslStream = new(client.GetStream(), false, new RemoteCertificateValidationCallback(CertValidationCallback));

        var certCollection = GetCertCollection(manifest.Archive, manifest.Preferences);

        sslStream.AuthenticateAsClient(host, certCollection, false);

        while (true)
        {
            var buffer = new byte[client.ReceiveBufferSize];
            await sslStream.ReadAsync(buffer);
            var data = Encoding.UTF8.GetString(buffer);

            foreach (Match match in Regex.Matches(data, @"<event(.|\s)*\/event>"))
            {
                try
                {
                    using var reader = XmlReader.Create(data, looseXmlReaderSettings);
                    var evt = Event.Parse(match.Value);
                }
                catch (OverflowException ex)
                {
                }
                catch (Exception ex)
                {
                }
            }
            await Task.Delay(500);
        }
    }

    private static X509CertificateCollection GetCertCollection(ZipArchive archive, Preferences manifest)
    {
        var pref = manifest.Preference.First(p => p.Name == PrefsKey);
        var certFileName = Path.GetFileName(pref.Entry.First(e => e.Key == CertLocationKey).Text);
        var prefEntry = manifest.Preference.First(e => e.Name.Contains("preference.pref"));

        var certEntry = archive.Entries.First(e => e.Name.Contains(certFileName));
        var cert = new X509Certificate(, pref.Entry.FirstOrDefault(e => e.Key == CertPasswordKey)?.Text);

        return new X509CertificateCollection() { cert };
    }

    // FIXME: Cert validation feedback
    private bool CertValidationCallback(object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors) => true;

    private (Preferences Preferences, ZipArchive Archive) GetPackageManifest()
    {
        var package = new ZipArchive(new FileStream(packagePath, FileMode.Open));
        var prefEntry = package.Entries.First(e => e.Name.Contains("preference.pref"));
        var prefStream = prefEntry.Open();
        var xmlStream = new StreamReader(prefStream);
        XmlSerializer serializer = new(typeof(Preferences));

        return ((Preferences)serializer.Deserialize(xmlStream)!, package);
    }
}