using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Logging;
using Sanierungsplaner.Desktop.Services;
using Sanierungsplaner.Sync;
namespace Sanierungsplaner.Desktop.Sync;
public sealed class LanSyncServer : IAsyncDisposable
{
 readonly IProjectStore store;readonly string folder;readonly DeviceRegistry registry;readonly X509Certificate2 certificate;
 public MeshAuthority Mesh {get; private set;}=null!;
 WebApplication? app;readonly SemaphoreSlim lifecycle=new(1,1);
 public sealed record LanAddress(string Address,int Prefix,string Name){public override string ToString()=>Name+" · "+Address;}
 public DeviceRegistry Registry=>registry;
 public bool Running=>app!=null;
 public string Url {get;private set;}="";
 public string Fingerprint=>Convert.ToHexString(SHA256.HashData(certificate.RawData));
 public LanSyncServer(IProjectStore store,string folder,X509Certificate2? testCertificate=null)
 {this.store=store;this.folder=folder;Directory.CreateDirectory(folder);registry=new(folder);certificate=testCertificate??LoadCertificate(folder);Mesh=new((JsonProjectStore)store,folder,registry,certificate);}
 public static IReadOnlyList<LanAddress> Addresses()=>NetworkInterface.GetAllNetworkInterfaces().Where(n=>n.OperationalStatus==OperationalStatus.Up&&n.NetworkInterfaceType!=NetworkInterfaceType.Loopback).SelectMany(n=>n.GetIPProperties().UnicastAddresses.Where(a=>SyncRules.PrivateIp(a.Address)).Select(a=>new LanAddress(a.Address.ToString(),a.PrefixLength,n.Name))).ToArray();
 public async Task Start(LanAddress address)
 {
  await lifecycle.WaitAsync();try
  {
   if(app!=null)return;
   var ip=IPAddress.Parse(address.Address);if(!SyncRules.PrivateIp(ip))throw new InvalidDataException("Nur private Heimnetz-Adressen sind erlaubt.");
   var b=WebApplication.CreateSlimBuilder(new WebApplicationOptions{Args=[]});b.Logging.ClearProviders();
   b.WebHost.ConfigureKestrel(o=>{o.AddServerHeader=false;o.Limits.MaxRequestBodySize=SyncRules.MaxBodyBytes;o.Limits.MaxConcurrentConnections=32;o.Limits.RequestHeadersTimeout=TimeSpan.FromSeconds(10);o.Listen(ip,SyncRules.Port,l=>{l.Protocols=HttpProtocols.Http1AndHttp2;l.UseHttps(certificate);});});
   var next=b.Build();
   next.Use(async(context,continueRequest)=>
   {
    context.Response.Headers.CacheControl="no-store";
    if(context.Connection.RemoteIpAddress is not { } peer||!SyncRules.SameSubnet(peer,ip,address.Prefix)){context.Response.StatusCode=403;return;}
    try{await continueRequest();}
    catch(UnauthorizedAccessException){context.Response.StatusCode=401;await context.Response.WriteAsJsonAsync(new{Error="Gerät oder Aktion nicht freigegeben."});}
    catch(InvalidDataException ex){context.Response.StatusCode=400;await context.Response.WriteAsJsonAsync(new{Error=ex.Message});}
    catch(JsonException){context.Response.StatusCode=400;}
    catch(IOException){context.Response.StatusCode=409;await context.Response.WriteAsJsonAsync(new{Error="Speicherkonflikt. Beide Versionen bleiben erhalten."});}
   });
   next.MapPost("/mesh/register",async(HttpContext c)=>
   {
    var r=await c.Request.ReadFromJsonAsync<MeshRegistration>()??throw new InvalidDataException();
    return registry.Authorized(Token(c),d=>Results.Json(Mesh.Register(d.Id,r,Url)));
   });
   next.MapPost("/mesh",async(HttpContext c)=>
   {
    var p=await c.Request.ReadFromJsonAsync<SignedPacket>()??throw new InvalidDataException();
    Mesh.Refresh(Url);return Results.Json(Mesh.Engine.Exchange(p));
   });
   next.MapPost("/pair",async(HttpContext c)=>{var request=await c.Request.ReadFromJsonAsync<PairRequest>()??throw new InvalidDataException("Leere Kopplungsanfrage.");registry.Claim(request);return Results.Ok(new{Pending=true});});
   next.MapGet("/identity",(HttpContext c)=>registry.Authorized(Token(c),d=>Results.Json(new DeviceIdentity(d.Id,d.Name,d.Role))));
   next.MapGet("/projects",(HttpContext c)=>registry.Authorized(Token(c),d=>Results.Json(store.Load())));
   next.MapGet("/deleted-projects",(HttpContext c)=>registry.Authorized(Token(c),d=>Results.Json(((JsonProjectStore)store).DeletedIds())));
   next.MapPost("/delete-project",async(HttpContext c)=>
   {
    var deletion=await c.Request.ReadFromJsonAsync<DeleteProject>()??throw new InvalidDataException("Leere Löschanfrage.");
    return registry.Authorized(Token(c),d=>
    {
     if(d.Role!="Tobias")throw new UnauthorizedAccessException();
     ((JsonProjectStore)store).Delete(deletion.Id,deletion.ExpectedRevision);
     return Results.Ok();
    });
   });
   next.MapPost("/projects",async(HttpContext c)=>
   {
    var upload=await c.Request.ReadFromJsonAsync<UploadProject>()??throw new InvalidDataException("Leeres Projekt.");
    return registry.Authorized(Token(c),d=>
    {
     var current=store.Load().SingleOrDefault(p=>p.Id==upload.Project.Id);
     SyncRules.AuthorizeUpload(d.Role,current,upload.Project);
     if(current?.Revision!=upload.ExpectedRevision) return Results.Conflict(new{Error="Das Projekt wurde am PC oder auf einem anderen Handy geändert."});
     var saved=upload.Project with{Revision=Guid.NewGuid(),UpdatedAt=DateTimeOffset.UtcNow};
     store.Save(saved,current?.Revision);
     File.AppendAllText(Path.Combine(folder,"sync-audit.jsonl"),JsonSerializer.Serialize(new{At=DateTimeOffset.UtcNow,Device=d.Id,d.Role,Project=saved.Id,saved.Revision})+Environment.NewLine);
     return Results.Json(new SyncReceipt(saved));
    });
   });
   try{await next.StartAsync();app=next;Url=$"https://{ip}:{SyncRules.Port}/";Mesh.Refresh(Url);File.WriteAllText(Path.Combine(folder,"auto-address.txt"),address.Address);}catch{await next.DisposeAsync();throw;}
  }finally{lifecycle.Release();}
 }
 public string Invite(string role)
 {
  if(!Running)throw new InvalidOperationException("Zuerst WLAN-Abgleich starten.");
  return "SP1:"+Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(new PairInvite(Url,Fingerprint,registry.Invite(role))));
 }
 public void DisableAutomaticStart(){File.Delete(Path.Combine(folder,"auto-address.txt"));}
 public async Task Stop()
 {
  await lifecycle.WaitAsync();try{registry.StopPairing();if(app!=null){await app.StopAsync();await app.DisposeAsync();app=null;Url="";}}finally{lifecycle.Release();}
 }
 public async ValueTask DisposeAsync(){await Stop();certificate.Dispose();lifecycle.Dispose();}
 static string Token(HttpContext c)
 {
  var value=c.Request.Headers.Authorization.ToString();if(!value.StartsWith("Bearer ",StringComparison.Ordinal))throw new UnauthorizedAccessException();return value[7..];
 }
 static X509Certificate2 LoadCertificate(string folder)
 {
  var certPath=Path.Combine(folder,"server.cer");var namePath=Path.Combine(folder,"server-key-name.txt");
  if(File.Exists(certPath))
  {
   var cert=X509CertificateLoader.LoadCertificateFromFile(certPath);
   using var key=CngKey.Open(File.ReadAllText(namePath));using var rsa=new RSACng(key);
   if(cert.NotAfter<=DateTime.Now)throw new InvalidDataException("Das WLAN-Zertifikat ist abgelaufen. Die Geräte müssen mit einem neuen Zertifikat gekoppelt werden.");
   return cert.CopyWithPrivateKey(rsa);
  }
  var name="Sanierungsplaner-WLAN-"+Guid.NewGuid().ToString("N");
  using var created=CngKey.Create(CngAlgorithm.Rsa,name,new CngKeyCreationParameters{ExportPolicy=CngExportPolicies.None,KeyUsage=CngKeyUsages.Signing,Parameters={new CngProperty("Length",BitConverter.GetBytes(3072),CngPropertyOptions.None)}});
  using var privateKey=new RSACng(created);
  var req=new CertificateRequest("CN=Sanierungsplaner Heimnetz",privateKey,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
  req.CertificateExtensions.Add(new X509BasicConstraintsExtension(false,false,0,true));
  req.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature,true));
  req.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection{new Oid("1.3.6.1.5.5.7.3.1")},true));
  using var issued=req.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5),DateTimeOffset.UtcNow.AddYears(5));
  File.WriteAllText(namePath,name);File.WriteAllBytes(certPath,issued.Export(X509ContentType.Cert));
  return issued.CopyWithPrivateKeyIfNeeded(privateKey);
 }
}
internal static class CertificateExtensions
{
 public static X509Certificate2 CopyWithPrivateKeyIfNeeded(this X509Certificate2 cert,RSA key)=>X509CertificateLoader.LoadCertificate(cert.RawData).CopyWithPrivateKey(key);
}
