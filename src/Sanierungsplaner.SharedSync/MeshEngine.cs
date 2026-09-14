using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Sanierungsplaner.Desktop.Models;
using Sanierungsplaner.Desktop.Services;
namespace Sanierungsplaner.Sync;

public sealed record MeshDevice(Guid Id,string Name,string Role,string Certificate,string Url,bool Revoked);
public sealed record MeshDirectory(long Version,Guid PcId,MeshDevice[] Devices);
public sealed record SignedDirectory(MeshDirectory Data,string Signature);
public sealed record MeshRegistration(string Certificate,string Url);
public sealed record MeshChange(Guid Id,Guid Author,Guid ProjectId,RenovationProject? Before,RenovationProject? After,bool Deleted,Guid[] Resolves);
public sealed record SignedChange(MeshChange Data,string Signature);
public sealed record MeshPacket(Guid Sender,long Time,string Nonce,SignedDirectory Directory,SignedChange[] Changes,Guid[] Known);
public sealed record SignedPacket(MeshPacket Data,string Signature);
public sealed record MeshState(SignedDirectory? Directory,SignedChange[] Changes,Guid[] Applied,RenovationProject[] Materialized);

public sealed class MeshEngine
{
 readonly object gate=new();readonly JsonProjectStore store;readonly string path;
 readonly X509Certificate2 key;readonly string authority;
 MeshState state;Guid self;readonly Dictionary<Guid,Guid[]> peerKnown=new();
 public Guid Self=>self;
 public string LastStatus {get;private set;}="Noch kein Geräteabgleich.";
 public static byte[] Bytes<T>(T data)=>JsonSerializer.SerializeToUtf8Bytes(data);
 public static string Sign<T>(T data,X509Certificate2 key){using var rsa=key.GetRSAPrivateKey()??throw new InvalidDataException();return Convert.ToBase64String(rsa.SignData(Bytes(data),HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1));}
 public static bool Verify<T>(T data,string signature,string cert)
 {try{using var c=X509CertificateLoader.LoadCertificate(Convert.FromBase64String(cert));using var rsa=c.GetRSAPublicKey();return rsa!=null&&rsa.VerifyData(Bytes(data),Convert.FromBase64String(signature),HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);}catch(Exception e)when(e is CryptographicException or FormatException or ArgumentException){return false;}}
 public MeshEngine(JsonProjectStore store,string folder,X509Certificate2 key,string authority,Guid self)
 {
  this.store=store;this.key=key;this.authority=authority;this.self=self;Directory.CreateDirectory(folder);path=Path.Combine(folder,"mesh-state.json");
  state=File.Exists(path)?JsonSerializer.Deserialize<MeshState>(File.ReadAllText(path))??throw new InvalidDataException():new(null,[],[],Convert.ToBase64String(key.RawData)==authority?[]:store.Load().ToArray());
  if(state.Directory!=null)CheckDirectory(state.Directory);
 }
 void CheckDirectory(SignedDirectory directory)
 {
  if(directory?.Data?.Devices==null||directory.Signature==null)throw new InvalidDataException();
  if(!Verify(directory.Data,directory.Signature,authority)||directory.Data.Devices.Length>101||directory.Data.Devices.Select(d=>d.Id).Distinct().Count()!=directory.Data.Devices.Length)throw new UnauthorizedAccessException("Geräteliste nicht vom PC bestätigt.");
  foreach(var d in directory.Data.Devices)
  {if(!SalesCredit.Recipients.Contains(d.Role)||d.Id==Guid.Empty)throw new InvalidDataException();using var cert=X509CertificateLoader.LoadCertificate(Convert.FromBase64String(d.Certificate));SyncRules.ValidateInvite(new(d.Url,Convert.ToHexString(SHA256.HashData(cert.RawData)),SyncRules.NewSecret()));}
 }
 public void SetDirectory(SignedDirectory directory)
 {
  lock(gate){CheckDirectory(directory);if(state.Directory!=null&&directory.Data.Version<state.Directory.Data.Version)return;state=state with{Directory=directory};Persist();}
 }
 public SignedDirectory? DirectorySnapshot{get{lock(gate)return state.Directory;}}
 MeshDevice Device(Guid id)=>state.Directory?.Data.Devices.SingleOrDefault(d=>d.Id==id&&!d.Revoked)??throw new UnauthorizedAccessException("Gerät nicht freigegeben oder widerrufen.");
 static bool Same(RenovationProject? a,RenovationProject? b)=>a==null||b==null?a==b:JsonSerializer.Serialize(a with{Revision=Guid.Empty,UpdatedAt=default})==JsonSerializer.Serialize(b with{Revision=Guid.Empty,UpdatedAt=default});
 void Persist(){var temp=path+".tmp";using(var f=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None)){JsonSerializer.Serialize(f,state);f.Flush(true);}File.Move(temp,path,true);}
 public void Capture()
 {
  lock(gate)
  {
   var device=Device(self);var material=state.Materialized.ToDictionary(p=>p.Id);
   foreach(var project in store.Load())
   {
    material.TryGetValue(project.Id,out var before);if(Same(before,project))continue;
    SyncRules.AuthorizeUpload(device.Role,before,project);
    Append(new(Guid.NewGuid(),self,project.Id,before,project,false,[]));material[project.Id]=project;
   }
   foreach(var id in store.DeletedIds())
   {
    if(state.Changes.Any(c=>c.Data.ProjectId==id&&c.Data.Deleted))continue;
    if(device.Role!="Tobias")throw new UnauthorizedAccessException("Nur Tobias darf löschen.");
    material.TryGetValue(id,out var before);Append(new(Guid.NewGuid(),self,id,before,null,true,[]));material.Remove(id);
   }
   state=state with{Materialized=material.Values.ToArray()};Persist();
  }
 }
 void Append(MeshChange change)
 {state=state with{Changes=state.Changes.Append(new SignedChange(change,Sign(change,key))).ToArray(),Applied=state.Applied.Append(change.Id).ToArray()};}
 public void Delete(RenovationProject project)
 {
  lock(gate){if(Device(self).Role!="Tobias")throw new UnauthorizedAccessException();store.Delete(project.Id,project.Revision);Capture();}
 }
 public SignedPacket Packet(Guid[]? known=null)
 {
  lock(gate)
  {
   Capture();var sent=new List<SignedChange>();var have=(known??[]).ToHashSet();var budget=6*1024*1024;
   foreach(var c in state.Changes.Where(c=>!have.Contains(c.Data.Id)))
   {
    var length=Bytes(c).Length;if(length>budget&&sent.Count==0)throw new InvalidDataException("Ein Projektstand ist zu groß für den Geräteabgleich (maximal 6 MB).");
    if(length>budget)break;sent.Add(c);budget-=length;
   }
   var p=new MeshPacket(self,DateTimeOffset.UtcNow.ToUnixTimeSeconds(),Guid.NewGuid().ToString("N"),state.Directory!,sent.ToArray(),state.Changes.Select(c=>c.Data.Id).ToArray());return new(p,Sign(p,key));
  }
 }
 public SignedPacket Exchange(SignedPacket packet)
 {
  lock(gate)
  {
   Receive(packet);return Packet(packet.Data.Known);
  }
 }
 public void Receive(SignedPacket packet)
 {
  lock(gate)
  {
   if(packet?.Data?.Directory==null||packet.Signature==null)throw new InvalidDataException();
   if(packet.Data.Known==null||packet.Data.Known.Length>50000||packet.Data.Changes==null)throw new InvalidDataException();
   CheckDirectory(packet.Data.Directory);
   var latest=state.Directory==null||packet.Data.Directory.Data.Version>state.Directory.Data.Version?packet.Data.Directory:state.Directory;
   var sender=latest.Data.Devices.SingleOrDefault(d=>d.Id==packet.Data.Sender&&!d.Revoked)??throw new UnauthorizedAccessException();
   if(Math.Abs(DateTimeOffset.UtcNow.ToUnixTimeSeconds()-packet.Data.Time)>300||!Verify(packet.Data,packet.Signature,sender.Certificate))throw new UnauthorizedAccessException("Absenderprüfung fehlgeschlagen. Geräteuhren prüfen.");
   peerKnown[packet.Data.Sender]=packet.Data.Known;SetDirectory(latest);Capture();
   var known=state.Changes.Select(c=>c.Data.Id).ToHashSet();var changes=state.Changes.ToList();
   foreach(var c in packet.Data.Changes)
   {
    if(known.Contains(c.Data.Id))continue;
    var author=latest.Data.Devices.SingleOrDefault(d=>d.Id==c.Data.Author&&!d.Revoked);
    if(author==null)continue;
    if(!Verify(c.Data,c.Signature,author.Certificate)||c.Data.Id==Guid.Empty||c.Data.ProjectId==Guid.Empty||c.Data.Resolves==null)throw new UnauthorizedAccessException("Änderungssignatur ungültig.");
    if(c.Data.Deleted&&author.Role!="Tobias"||c.Data.Resolves.Length>0&&author.Id!=latest.Data.PcId)throw new UnauthorizedAccessException();
    if(!c.Data.Deleted){c.Data.After?.Validate();if(c.Data.After==null||c.Data.After.Id!=c.Data.ProjectId)throw new InvalidDataException();}
    changes.Add(c);known.Add(c.Data.Id);
   }
   state=state with{Changes=changes.ToArray()};Persist();Apply();
  }
 }
 void Apply()
 {
  var applied=state.Applied.ToHashSet();var material=state.Materialized.ToDictionary(p=>p.Id);
  foreach(var signed in state.Changes.Where(c=>!applied.Contains(c.Data.Id)).OrderBy(c=>c.Data.Deleted?0:1))
  {
   var c=signed.Data;var author=state.Directory!.Data.Devices.SingleOrDefault(d=>d.Id==c.Author&&!d.Revoked);if(author==null)continue;
   try
   {
    var current=store.Load().SingleOrDefault(p=>p.Id==c.ProjectId);
    if(c.Deleted){store.Delete(c.ProjectId,null,true);material.Remove(c.ProjectId);applied.Add(c.Id);continue;}
    if(store.DeletedIds().Contains(c.ProjectId)){applied.Add(c.Id);continue;}
    var resolvingKnownVersion=c.Author==state.Directory!.Data.PcId&&c.Resolves.Length>0&&
      (Same(current,c.Before)||c.Resolves.Any(id=>state.Changes.Any(old=>old.Data.Id==id&&Same(old.Data.After,current))));
    var next=resolvingKnownVersion ? c.After : Same(current,c.After)?current:Same(current,c.Before)?c.After:Merge(c.Before,current,c.After!);
    if(next==null)continue;
    SyncRules.AuthorizeUpload(author.Role,current,next);
    if(current==null||!Same(current,next))store.ReplaceFromSync(next,current?.Revision);
    material[c.ProjectId]=next;applied.Add(c.Id);
    if(c.Author==state.Directory.Data.PcId)foreach(var id in c.Resolves)applied.Add(id);
   }
   catch(Exception e)when(e is IOException or InvalidDataException or UnauthorizedAccessException){/* Keep the signed change for explicit conflict resolution; never discard it. */}
  }
  state=state with{Applied=applied.ToArray(),Materialized=material.Values.ToArray()};Persist();
  LastStatus="Letzter Geräteabgleich: "+DateTime.Now.ToString("dd.MM.yyyy HH:mm")+(Conflicts.Length>0?$" · {Conflicts.Length} Konflikt(e) am PC prüfen":"");
 }
 // Three-way merging keeps unrelated edits and additions; competing edits stay pending.
 static RenovationProject? Merge(RenovationProject? baseline,RenovationProject? current,RenovationProject incoming)
 {
  if(baseline==null||current==null)return null;
  try
  {
   T Value<T>(T b,T l,T r)=>EqualityComparer<T>.Default.Equals(l,r)||EqualityComparer<T>.Default.Equals(b,r)?l:EqualityComparer<T>.Default.Equals(b,l)?r:throw new InvalidDataException();
   T[] Rows<T>(T[] b,T[] l,T[] r,Func<T,Guid> id)
   {
    var bd=b.ToDictionary(id);var ld=l.ToDictionary(id);var rd=r.ToDictionary(id);var list=new List<T>();
    foreach(var k in ld.Keys.Concat(rd.Keys).Distinct())
    {
     bd.TryGetValue(k,out var bv);ld.TryGetValue(k,out var lv);rd.TryGetValue(k,out var rv);
     var value=Value(bv,lv,rv);if(value!=null)list.Add(value);
    }
    return list.ToArray();
   }
   var next=current with{Revision=Guid.NewGuid(),UpdatedAt=DateTimeOffset.UtcNow,
    Name=Value(baseline.Name,current.Name,incoming.Name),Address=Value(baseline.Address,current.Address,incoming.Address),Notes=Value(baseline.Notes,current.Notes,incoming.Notes),Budget=Value(baseline.Budget,current.Budget,incoming.Budget),
    Items=Rows(baseline.Items,current.Items,incoming.Items,p=>p.Id),Reimbursements=Rows(baseline.Reimbursements,current.Reimbursements,incoming.Reimbursements,p=>p.Id),Credits=Rows(baseline.Credits,current.Credits,incoming.Credits,p=>p.Id),IncomingRepayments=Rows(baseline.IncomingRepayments,current.IncomingRepayments,incoming.IncomingRepayments,p=>p.Id)};
   next.Validate();return next;
  }
  catch(InvalidDataException){return null;}
 }
 public SignedChange[] Conflicts{get{lock(gate)return state.Changes.Where(c=>!state.Applied.Contains(c.Data.Id)).ToArray();}}
 public void Resolve(Guid changeId,bool incoming)
 {
  lock(gate)
  {
   if(self!=state.Directory?.Data.PcId)throw new UnauthorizedAccessException();Capture();
   var c=Conflicts.Single(c=>c.Data.Id==changeId).Data;var current=store.Load().SingleOrDefault(p=>p.Id==c.ProjectId)??throw new InvalidDataException("Projekt nicht vorhanden.");
   var next=incoming?c.After??throw new InvalidDataException():current;
   SyncRules.AuthorizeUpload("Tobias",current,next);
   next=next with{Revision=Guid.NewGuid(),UpdatedAt=DateTimeOffset.UtcNow};store.ReplaceFromSync(next,current.Revision);
   Append(new(Guid.NewGuid(),self,c.ProjectId,current,next,false,[changeId]));state=state with{Applied=state.Applied.Append(changeId).ToArray(),Materialized=state.Materialized.Where(p=>p.Id!=next.Id).Append(next).ToArray()};Persist();
  }
 }
 public async Task<int> Synchronize()
 {
  var count=0;MeshDevice[] peers;lock(gate){Capture();peers=state.Directory!.Data.Devices.Where(d=>d.Id!=self&&!d.Revoked).ToArray();}
  foreach(var peer in peers)
  {
   try
   {
    using var cert=X509CertificateLoader.LoadCertificate(Convert.FromBase64String(peer.Certificate));
    using var handler=new HttpClientHandler{UseProxy=false,AllowAutoRedirect=false,ServerCertificateCustomValidationCallback=(_,c,_,_)=>c!=null&&c.RawData.SequenceEqual(cert.RawData)};
    using var http=new HttpClient(handler){Timeout=TimeSpan.FromSeconds(5),MaxResponseContentBufferSize=SyncRules.MaxBodyBytes};
    Guid[] known;lock(gate)known=peerKnown.GetValueOrDefault(peer.Id,[]);
    using var content=new ByteArrayContent(Bytes(Packet(known)));if(content.Headers.ContentLength>SyncRules.MaxBodyBytes)throw new InvalidDataException("Abgleichdaten größer als 8 MB. Bitte am PC prüfen.");content.Headers.ContentType=new MediaTypeHeaderValue("application/json");
    using var response=await http.PostAsync(new Uri(new Uri(peer.Url),"mesh"),content);response.EnsureSuccessStatusCode();
    Receive(await response.Content.ReadFromJsonAsync<SignedPacket>()??throw new InvalidDataException());count++;
   }
   catch(Exception e)when(e is HttpRequestException or IOException or OperationCanceledException){/* Offline peers are retried on the next scheduled attempt. */}
  }
  lock(gate){LastStatus=count>0?$"Abgleich mit {count} Gerät(en): {DateTime.Now:dd.MM.yyyy HH:mm}":"Kein freigegebenes Gerät erreichbar. Änderungen bleiben gespeichert.";}
  return count;
 }
}
