using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Sanierungsplaner.Sync;
using Sanierungsplaner.Desktop.Models;
using Sanierungsplaner.Desktop.Services;
internal static class MeshTests
{
 public static void Run(string root)
 {
  void Check(bool ok,string name){if(!ok)throw new Exception("Mesh: "+name);}
  X509Certificate2 Cert(){using var rsa=RSA.Create(2048);var req=new CertificateRequest("CN=Mesh test",rsa,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);return req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1),DateTimeOffset.UtcNow.AddDays(1));}
  using var pcKey=Cert();using var leaKey=Cert();using var jenKey=Cert();using var ownerKey=Cert();
  var pcId=Guid.NewGuid();var leaId=Guid.NewGuid();var jenId=Guid.NewGuid();var ownerId=Guid.NewGuid();
  MeshDevice Device(Guid id,string role,X509Certificate2 c)=>new(id,role,role,Convert.ToBase64String(c.RawData),"https://192.168.1.2:58443/",false);
  var data=new MeshDirectory(1,pcId,[Device(pcId,"Tobias",pcKey),Device(leaId,"Lea",leaKey),Device(jenId,"Jennifer",jenKey),Device(ownerId,"Tobias",ownerKey)]);
  var directory=new SignedDirectory(data,MeshEngine.Sign(data,pcKey));
  var pcStore=new JsonProjectStore(Path.Combine(root,"pc"));var leaStore=new JsonProjectStore(Path.Combine(root,"lea"));var jenStore=new JsonProjectStore(Path.Combine(root,"jen"));var ownerStore=new JsonProjectStore(Path.Combine(root,"owner"));
  MeshEngine Engine(JsonProjectStore store,string name,X509Certificate2 c,Guid id){var e=new MeshEngine(store,Path.Combine(root,name+"state"),c,Convert.ToBase64String(pcKey.RawData),id);e.SetDirectory(directory);return e;}
  var pc=Engine(pcStore,"pc",pcKey,pcId);var lea=Engine(leaStore,"lea",leaKey,leaId);var jen=Engine(jenStore,"jen",jenKey,jenId);var owner=Engine(ownerStore,"owner",ownerKey,ownerId);
  var now=DateTimeOffset.UtcNow;var project=new RenovationProject(Guid.NewGuid(),Guid.NewGuid(),"Echtes Testhaus","","",now,now){Items=[new CostItem(Guid.NewGuid(),"Basis","","",1,"Stück",100,"Gekauft",new Payments(Lea:100))]};pcStore.Save(project,null);
  lea.Receive(pc.Packet());jen.Receive(pc.Packet());owner.Receive(pc.Packet());Check(leaStore.Load().Count==1,"Initial PC transfer");
  void Add(JsonProjectStore store,string title){var p=store.Load().Single();store.Save(p with{Revision=Guid.NewGuid(),Items=p.Items.Append(new CostItem(Guid.NewGuid(),title,"","",1,"Stück",10,"Gekauft",new Payments())).ToArray()},p.Revision);}
  Add(leaStore,"Lea Einkauf");Add(jenStore,"Jennifer Einkauf");
  lea.Receive(jen.Packet());jen.Receive(lea.Packet());Check(leaStore.Load().Single().Items.Length==3&&jenStore.Load().Single().Items.Length==3,"Offline-PC phone-to-phone concurrent additions");
  pc.Receive(lea.Packet());Check(pcStore.Load().Single().Items.Length==3,"PC receives relayed origin signatures");
  var current=pcStore.Load().Single();pc.Receive(lea.Packet());Check(pcStore.Load().Single().Items.Length==3,"Replay idempotence");
  void Note(JsonProjectStore store,string note){var p=store.Load().Single();store.Save(p with{Revision=Guid.NewGuid(),Notes=note},p.Revision);}
  Note(leaStore,"Lea Entwurf");Note(jenStore,"Jennifer Entwurf");lea.Receive(jen.Packet());Check(lea.Conflicts.Length>0&&leaStore.Load().Single().Notes=="Lea Entwurf","Conflicting edits retained");
  pc.Receive(lea.Packet());Check(pc.Conflicts.Length>0,"Conflict reaches PC");var conflict=pc.Conflicts.First();pc.Resolve(conflict.Data.Id,false);Check(pc.Conflicts.Length==0,"PC can resolve conflict");lea.Receive(pc.Packet());jen.Receive(pc.Packet());Check(leaStore.Load().Single().Notes==pcStore.Load().Single().Notes&&jenStore.Load().Single().Notes==pcStore.Load().Single().Notes,"PC conflict resolution reaches both phones");
  var evil=new MeshChange(Guid.NewGuid(),leaId,project.Id,null,null,true,[]);var packet=lea.Packet().Data with{Changes=[new(evil,MeshEngine.Sign(evil,leaKey))]};
  try{pc.Receive(new(packet,MeshEngine.Sign(packet,leaKey)));throw new Exception("Forged deletion accepted");}catch(UnauthorizedAccessException){}
  Check(pcStore.Load().Count==1,"Non-Tobias deletion denied");
  owner.Receive(pc.Packet());owner.Delete(ownerStore.Load().Single());lea.Receive(owner.Packet());pc.Receive(lea.Packet());jen.Receive(pc.Packet());Check(pcStore.Load().Count==0&&jenStore.Load().Count==0&&leaStore.Load().Count==0,"Tobias offline deletion relays through Lea to PC and other phone");
  try{pcStore.Save(project,null);throw new Exception("Resurrection accepted");}catch(IOException){}
  var bad=directory with{Data=data with{Version=99}};try{lea.SetDirectory(bad);throw new Exception("Forged directory accepted");}catch(UnauthorizedAccessException){}
  var revokedData=data with{Version=2,Devices=data.Devices.Select(d=>d.Id==jenId?d with{Revoked=true}:d).ToArray()};var revoked=new SignedDirectory(revokedData,MeshEngine.Sign(revokedData,pcKey));pc.SetDirectory(revoked);
  try{pc.Receive(jen.Packet());throw new Exception("Revoked sender accepted");}catch(UnauthorizedAccessException){}
  pc.SetDirectory(directory);Check(pc.DirectorySnapshot!.Data.Version==2,"Directory rollback prevented");
  var restored=new MeshEngine(pcStore,Path.Combine(root,"pcstate"),pcKey,Convert.ToBase64String(pcKey.RawData),pcId);Check(restored.DirectorySnapshot!.Data.Version==2&&pcStore.DeletedIds().Contains(project.Id),"Restart persistence");
  Check(!MeshSchedule.IsNight(new DateTime(2026,1,1,21,59,0))&&MeshSchedule.IsNight(new DateTime(2026,1,1,22,0,0))&&MeshSchedule.IsNight(new DateTime(2026,1,2,2,59,0))&&!MeshSchedule.IsNight(new DateTime(2026,1,2,3,0,0)),"Night window boundaries");
  Console.WriteLine("PASS Mesh: phone relay without PC, simultaneous purchases, replay, conflict retention, PC resolution, signed roles, owner deletion propagation, revocation, rollback protection, persistence and 22–03 window.");
 }
}
