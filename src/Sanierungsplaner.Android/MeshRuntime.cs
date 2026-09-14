using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Sanierungsplaner.Sync;
using Sanierungsplaner.Desktop.Services;
using global::Android.Content;
using global::Android.Net;
namespace Sanierungsplaner.AndroidApp;
public static class MeshRuntime
{
 public static readonly SemaphoreSlim Gate=new(1,1);
 static PeerTlsServer? server;static X509Certificate2? certificate;
 public static MeshEngine? Engine{get;private set;}
 public static string Status{get;private set;}="Handy-Abgleich noch nicht eingerichtet.";
 static string? activeUrl;
 static string Root(Context context)=>context.FilesDir!.AbsolutePath;
 public static (IPAddress Address,int Prefix) Address(Context context)
 {
  var cm=(ConnectivityManager)context.GetSystemService(Context.ConnectivityService)!;
  var network=cm.ActiveNetwork;var caps=cm.GetNetworkCapabilities(network);
  if(caps==null||!caps.HasTransport(TransportType.Wifi))throw new IOException("Bitte mit dem Heim-WLAN verbinden.");
  var links=cm.GetLinkProperties(network)?.LinkAddresses;
  if(links!=null)foreach(var link in links)if(IPAddress.TryParse(link.Address?.HostAddress,out var ip)&&SyncRules.PrivateIp(ip))return(ip,link.PrefixLength);
  throw new IOException("Keine private WLAN-Adresse vorhanden.");
 }
 static X509Certificate2 Certificate(string root,SecureBindingStore secure)
 {
  const string name="mesh-key.encrypted";
  if(File.Exists(System.IO.Path.Combine(root,name)))return X509CertificateLoader.LoadPkcs12(secure.Unseal(name),null,X509KeyStorageFlags.EphemeralKeySet|X509KeyStorageFlags.Exportable);
  using var rsa=RSA.Create(3072);var request=new CertificateRequest("CN=Sanierungsplaner Handy",rsa,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
  request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature,true));
  using var issued=request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1),DateTimeOffset.UtcNow.AddYears(5));var pfx=issued.Export(X509ContentType.Pfx);secure.Seal(name,pfx);
  return X509CertificateLoader.LoadPkcs12(pfx,null,X509KeyStorageFlags.EphemeralKeySet|X509KeyStorageFlags.Exportable);
 }
 public static async Task<int> Tick(Context context)
 {
  await Gate.WaitAsync();
  try
  {
   var root=Root(context);var secure=new SecureBindingStore(root);var binding=secure.Load();
   if(binding==null){Status="Zuerst am PC koppeln und freigeben.";return 0;}
   var address=Address(context);var url=$"https://{address.Address}:{SyncRules.Port}/";
   certificate??=Certificate(root,secure);
   var store=new JsonProjectStore(System.IO.Path.Combine(root,"Projects"));var folder=System.IO.Path.Combine(root,"Mesh");Directory.CreateDirectory(folder);
   var anchorFile=System.IO.Path.Combine(folder,"authority.cer");
   if(Engine==null&&binding.DeviceId!=null&&File.Exists(anchorFile)){
    var anchor=File.ReadAllText(anchorFile);using var pc=X509CertificateLoader.LoadCertificate(Convert.FromBase64String(anchor));
    if(Convert.ToHexString(SHA256.HashData(pc.RawData))!=binding.Fingerprint)throw new UnauthorizedAccessException("PC-Zentrale geändert. Bitte erneut koppeln.");
    Engine=new(store,folder,certificate,anchor,binding.DeviceId.Value);
   }
   try
   {
    using var client=new SyncClient(binding);var identity=await client.Identity();
    binding=binding with{Role=identity.Role,DeviceId=identity.Id};secure.Save(binding);
    if(Engine==null)await client.Synchronize(store,System.IO.Path.Combine(root,"Sync"),(_,_)=>Task.FromResult(ConflictChoice.Cancel));
    var directory=await client.RegisterMesh(new(Convert.ToBase64String(certificate.RawData),url));
    var authority=directory.Data.Devices.Single(d=>d.Id==directory.Data.PcId).Certificate;
    using var pcCert=X509CertificateLoader.LoadCertificate(Convert.FromBase64String(authority));
    if(Convert.ToHexString(SHA256.HashData(pcCert.RawData))!=binding.Fingerprint)throw new UnauthorizedAccessException("Unbekannte PC-Zentrale.");
    if(Engine==null){File.WriteAllText(anchorFile,authority);Engine=new(store,folder,certificate,authority,identity.Id);}
    Engine.SetDirectory(directory);
   }
   catch(UnauthorizedAccessException)
   {
    secure.Save(binding with{Role=null});if(server!=null){await server.DisposeAsync();server=null;}Engine=null;Status="PC-Freigabe fehlt oder wurde widerrufen.";throw;
   }
   catch(Exception e)when(e is HttpRequestException or TaskCanceledException or IOException)
   {if(Engine==null)throw new IOException("Für die erste Einrichtung muss die neue PC-App erreichbar sein. Danach geht der Handy-Abgleich auch ohne PC.",e);}
   if(Engine==null)throw new IOException("Geräteabgleich nicht eingerichtet.");
   if(server==null||activeUrl!=url)
   {
    if(server!=null)await server.DisposeAsync();
    server=new(certificate,address.Address,address.Prefix,Engine);server.Start();activeUrl=url;
   }
   var count=await Engine.Synchronize();Status=Engine.LastStatus;return count;
  }
  catch(Exception e)
  {
   if(e is UnauthorizedAccessException)
   {
    var secure=new SecureBindingStore(Root(context));var saved=secure.Load();if(saved!=null)secure.Save(saved with{Role=null});
    if(server!=null)await server.DisposeAsync();server=null;Engine=null;
   }
   Status=e.Message;throw;
  }
  finally{Gate.Release();}
 }
 public static async Task Stop()
 {
  await Gate.WaitAsync();try{if(server!=null)await server.DisposeAsync();server=null;Engine=null;certificate?.Dispose();certificate=null;activeUrl=null;}finally{Gate.Release();}
 }
}
