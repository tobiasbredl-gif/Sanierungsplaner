using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Sanierungsplaner.Sync;
using Sanierungsplaner.Desktop.Services;
namespace Sanierungsplaner.Desktop.Sync;
public sealed class MeshAuthority
{
 readonly object gate=new();readonly string folder;readonly DeviceRegistry registry;readonly X509Certificate2 certificate;readonly Guid id;
 Dictionary<Guid,MeshRegistration> registrations;long version;
 public MeshEngine Engine{get;}
 public MeshAuthority(JsonProjectStore store,string folder,DeviceRegistry registry,X509Certificate2 certificate)
 {
  this.folder=folder;this.registry=registry;this.certificate=certificate;
  var idFile=Path.Combine(folder,"mesh-pc-id.txt");if(!File.Exists(idFile))File.WriteAllText(idFile,Guid.NewGuid().ToString());id=Guid.Parse(File.ReadAllText(idFile));
  var path=Path.Combine(folder,"mesh-devices.json");registrations=File.Exists(path)?JsonSerializer.Deserialize<Dictionary<Guid,MeshRegistration>>(File.ReadAllText(path))??[]:[];
  Engine=new(store,Path.Combine(folder,"Mesh"),certificate,Convert.ToBase64String(certificate.RawData),id);version=Engine.DirectorySnapshot?.Data.Version??0;
 }
 public SignedDirectory Register(Guid device,MeshRegistration registration,string url)
 {
  lock(gate)
  {
   using var cert=X509CertificateLoader.LoadCertificate(Convert.FromBase64String(registration.Certificate));
   SyncRules.ValidateInvite(new(registration.Url,Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(cert.RawData)),SyncRules.NewSecret()));
   using var rsa=cert.GetRSAPublicKey();if(rsa==null||rsa.KeySize<2048)throw new InvalidDataException("Geräteschlüssel ungültig.");
   registrations[device]=registration;var temp=Path.Combine(folder,"mesh-devices.json.tmp");File.WriteAllText(temp,JsonSerializer.Serialize(registrations));File.Move(temp,Path.Combine(folder,"mesh-devices.json"),true);
   return Refresh(url);
  }
 }
 public SignedDirectory Refresh(string url)
 {
  lock(gate)
  {
   var devices=new List<MeshDevice>{new(id,"PC-Zentrale","Tobias",Convert.ToBase64String(certificate.RawData),url,false)};
   foreach(var d in registry.Devices)if(registrations.TryGetValue(d.Id,out var r))devices.Add(new(d.Id,d.Name,d.Role,r.Certificate,r.Url,d.Revoked));
   var previous=Engine.DirectorySnapshot;
   if(previous!=null&&JsonSerializer.Serialize(previous.Data.Devices)==JsonSerializer.Serialize(devices))return previous;
   version=Math.Max(version+1,DateTimeOffset.UtcNow.UtcTicks);
   var data=new MeshDirectory(version,id,devices.ToArray());var signed=new SignedDirectory(data,MeshEngine.Sign(data,certificate));Engine.SetDirectory(signed);return signed;
  }
 }
}
