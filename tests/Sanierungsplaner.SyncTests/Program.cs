using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Sanierungsplaner.Sync;
using Sanierungsplaner.Desktop.Sync;
using Sanierungsplaner.Desktop.Services;
using Sanierungsplaner.Desktop.Models;
if(args.Length==2&&args[0]=="--emulator"){await EmulatorHost.Run(args[1]);return;}
var root=Path.Combine(Path.GetTempPath(),"Sanierungsplaner-SyncTests",Guid.NewGuid().ToString("N"));
void Check(bool ok,string label){if(!ok)throw new Exception(label);}
void Reject(Action action){try{action();}catch(Exception e)when(e is UnauthorizedAccessException or InvalidDataException or IOException){return;}throw new Exception("Expected rejection");}
async Task RejectAsync(Func<Task> action){try{await action();}catch(Exception e)when(e is UnauthorizedAccessException or InvalidDataException or IOException or HttpRequestException){return;}throw new Exception("Expected rejection");}
try
{
 var time=DateTimeOffset.UtcNow;var registry=new DeviceRegistry(Path.Combine(root,"registry"),()=>time);
 var token=SyncRules.NewSecret();var secret=registry.Invite("Tobias");registry.Claim(new(secret,"Testhandy",token));Reject(()=>registry.Authorized(token,d=>d.Role));Reject(()=>registry.Claim(new(secret,"Replay",SyncRules.NewSecret())));
 var id=registry.Pending.Single().Id;registry.Approve(id);Check(registry.Authorized(token,d=>d.Role)=="Tobias","PC assigns role");
 registry.Revoke(id);Reject(()=>registry.Authorized(token,d=>d.Role));
 var expired=registry.Invite("Lea");time=time.AddMinutes(6);Reject(()=>registry.Claim(new(expired,"Expired",SyncRules.NewSecret())));
 var fresh=registry.Invite("Lea");registry.Claim(new(fresh,"Pending",SyncRules.NewSecret()));time=time.AddMinutes(6);Reject(()=>registry.Approve(registry.Pending.FirstOrDefault()?.Id??Guid.NewGuid()));
 Check(!SyncRules.PrivateIp(IPAddress.Parse("8.8.8.8"))&&!SyncRules.SameSubnet(IPAddress.Parse("192.168.2.1"),IPAddress.Parse("192.168.1.1"),24),"Public and foreign subnet rejected");
 var address=LanSyncServer.Addresses().FirstOrDefault()??throw new Exception("Private LAN interface required for TLS test");
 using var rsa=RSA.Create(2048);var req=new CertificateRequest("CN=Sync test",rsa,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);using var cert=req.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1),DateTimeOffset.UtcNow.AddDays(1));
 var pc=new JsonProjectStore(Path.Combine(root,"PC"));await using var server=new LanSyncServer(pc,Path.Combine(root,"Server"));await server.Start(address);
 var invitation=SyncClient.Decode(server.Invite("Lea"));var binding=new ClientBinding(invitation.Url,invitation.Fingerprint,SyncRules.NewSecret(),null,null);using var client=new SyncClient(binding);
 await RejectAsync(()=>client.Identity());await client.Claim(invitation.Secret,"Lea-Test");await RejectAsync(()=>client.Identity());server.Registry.Approve(server.Registry.Pending.Single().Id);Check((await client.Identity()).Role=="Lea","TLS pairing and PC approval");
 using(var wrong=new SyncClient(binding with{Fingerprint=new string('0',64)}))await RejectAsync(()=>wrong.Identity());
 var now=DateTimeOffset.UtcNow;var project=new RenovationProject(Guid.NewGuid(),Guid.NewGuid(),"Gemeinsames Haus","","",now,now){Items=[new CostItem(Guid.NewGuid(),"Estrich","","",1,"Stück",100,"Gekauft",new Payments(Lea:100))]};
 var phone=new JsonProjectStore(Path.Combine(root,"Phone"));phone.Save(project,null);
 await client.Synchronize(phone,Path.Combine(root,"State"),(_,_)=>Task.FromResult(ConflictChoice.Cancel));Check(pc.Load().Single().Items[0].Total==100,"Authenticated push");
 var downloaded=phone.Load().Single();Check(downloaded.Revision==pc.Load().Single().Revision,"Receipt and revisions");
 var malicious=downloaded with{Reimbursements=[new Reimbursement(Guid.NewGuid(),"Lea",50,DateOnly.FromDateTime(now.Date),now,"")]};
 using var handler=new HttpClientHandler{ServerCertificateCustomValidationCallback=(_,c,_,_)=>c!=null&&Convert.ToHexString(SHA256.HashData(c.RawData))==binding.Fingerprint,UseProxy=false};using var raw=new HttpClient(handler){BaseAddress=new Uri(binding.Url)};raw.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",binding.Token);
 using(var denied=await raw.PostAsJsonAsync("projects",new UploadProject(malicious,downloaded.Revision)))Check(denied.StatusCode==HttpStatusCode.Unauthorized,"Lea cannot upload reimbursement despite crafted request");Check(pc.Load().Single().Reimbursements.Length==0,"Unauthorized upload did not mutate PC");
 var localEdit=downloaded with{Revision=Guid.NewGuid(),Notes="Handyänderung"};phone.Save(localEdit,downloaded.Revision);var remoteEdit=pc.Load().Single();pc.Save(remoteEdit with{Revision=Guid.NewGuid(),Notes="PC-Änderung"},remoteEdit.Revision);
 await RejectAsync(()=>client.Synchronize(phone,Path.Combine(root,"State"),(_,_)=>Task.FromResult(ConflictChoice.Cancel)));Check(phone.Load().Single().Notes=="Handyänderung"&&pc.Load().Single().Notes=="PC-Änderung","Conflict preserves both");
 await client.Synchronize(phone,Path.Combine(root,"State"),(_,_)=>Task.FromResult(ConflictChoice.Pc));Check(phone.Load().Single().Notes=="PC-Änderung","Explicit conflict resolution");
 var ownerInvite=SyncClient.Decode(server.Invite("Tobias"));var ownerBinding=new ClientBinding(ownerInvite.Url,ownerInvite.Fingerprint,SyncRules.NewSecret(),null,null);using var ownerClient=new SyncClient(ownerBinding);await ownerClient.Claim(ownerInvite.Secret,"Tobias-Test");server.Registry.Approve(server.Registry.Pending.Single().Id);
 var current=phone.Load().Single();phone.Save(current with{Revision=Guid.NewGuid(),Reimbursements=[new Reimbursement(Guid.NewGuid(),"Lea",100,DateOnly.FromDateTime(now.Date),now,"")]},current.Revision);
 await ownerClient.Synchronize(phone,Path.Combine(root,"State"),(_,_)=>Task.FromResult(ConflictChoice.Cancel));Check(pc.Load().Single().Reimbursements.Length==1,"Tobias reimbursement accepted");
 var device=(await ownerClient.Identity()).Id;server.Registry.Revoke(device);await RejectAsync(()=>ownerClient.Identity());await RejectAsync(()=>ownerClient.Synchronize(phone,Path.Combine(root,"State"),(_,_)=>Task.FromResult(ConflictChoice.Phone)));
 Console.WriteLine("PASS: one-use/expired pairing, pending approval, assigned roles, TLS pin mismatch, unauthenticated denial, forged reimbursement denial, authorized sync, conflicts, owner operation and revocation.");
}
finally{var keyPath=Path.Combine(root,"Server","server-key-name.txt");if(File.Exists(keyPath)){using var key=CngKey.Open(File.ReadAllText(keyPath));key.Delete();}if(Directory.Exists(root))Directory.Delete(root,true);}
