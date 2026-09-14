using System.IO;
using System.Text.Json;
using Sanierungsplaner.Desktop.Models;
using Sanierungsplaner.Sync;
namespace Sanierungsplaner.Desktop.Sync;
public sealed class DeviceRegistry
{
 readonly object gate=new(); readonly string path;readonly Func<DateTimeOffset> now;
 List<DeviceRecord> devices;
 string? inviteHash; string? inviteRole; DateTimeOffset inviteExpires;
 public sealed record PendingDevice(Guid Id,string Name,string Role,string TokenHash,DateTimeOffset Expires);
 readonly List<PendingDevice> pending=[];
 public DeviceRegistry(string folder,Func<DateTimeOffset>? clock=null)
 {
  now=clock??(()=>DateTimeOffset.UtcNow);Directory.CreateDirectory(folder);path=Path.Combine(folder,"devices.json");
  devices=File.Exists(path)?JsonSerializer.Deserialize<List<DeviceRecord>>(File.ReadAllText(path))??throw new InvalidDataException("Ungültige Gerätefreigaben."):[];
  if(devices.Any(d=>d.Id==Guid.Empty||!SalesCredit.Recipients.Contains(d.Role)||d.TokenHash.Length!=64)||devices.Select(d=>d.Id).Distinct().Count()!=devices.Count)throw new InvalidDataException("Ungültige Gerätefreigaben.");
 }
 public IReadOnlyList<DeviceRecord> Devices { get {lock(gate)return devices.ToArray();} }
 public IReadOnlyList<PendingDevice> Pending {get{lock(gate){pending.RemoveAll(p=>p.Expires<=now());return pending.ToArray();}}}
 public string Invite(string role)
 {
  if(!SalesCredit.Recipients.Contains(role))throw new ArgumentException("Unbekannte Rolle.");
  lock(gate){var secret=SyncRules.NewSecret();inviteHash=SyncRules.Hash(secret);inviteRole=role;inviteExpires=now().AddMinutes(5);return secret;}
 }
 public void Claim(PairRequest request)
 {
  if(!SyncRules.ValidSecret(request.Secret)||!SyncRules.ValidSecret(request.Token)||string.IsNullOrWhiteSpace(request.DeviceName)||request.DeviceName.Length>80)throw new UnauthorizedAccessException();
  lock(gate)
  {
   if(inviteHash==null||inviteExpires<=now()||SyncRules.Hash(request.Secret)!=inviteHash)throw new UnauthorizedAccessException("Kopplungscode ungültig oder abgelaufen.");
   var hash=SyncRules.Hash(request.Token);if(devices.Any(d=>d.TokenHash==hash)||pending.Any(d=>d.TokenHash==hash))throw new UnauthorizedAccessException();
   pending.RemoveAll(p=>p.Expires<=now());
   if(pending.Count>=8)throw new InvalidDataException("Zu viele offene Kopplungen.");
   pending.Add(new(Guid.NewGuid(),request.DeviceName.Trim(),inviteRole!,hash,inviteExpires));inviteHash=null;inviteRole=null;
  }
 }
 public void Approve(Guid id)
 {
  lock(gate)
  {
   var p=pending.SingleOrDefault(p=>p.Id==id&&p.Expires>now())??throw new InvalidDataException("Kopplung abgelaufen.");
   if(devices.Count>=100)throw new InvalidDataException("Maximal 100 Gerätefreigaben.");
   var next=devices.Append(new DeviceRecord(p.Id,p.Name,p.Role,p.TokenHash,false,now())).ToList();Save(next);devices=next;pending.Remove(p);
  }
 }
 public void Deny(Guid id){lock(gate)pending.RemoveAll(p=>p.Id==id);}
 public void Revoke(Guid id){lock(gate){var next=devices.Select(d=>d.Id==id?d with{Revoked=true}:d).ToList();Save(next);devices=next;}}
 public T Authorized<T>(string token,Func<DeviceRecord,T> action)
 {
  // Revocation and the entire read/write operation share this lock: no post-revocation write race.
  lock(gate){var d=Find(token);return action(d);}
 }
 DeviceRecord Find(string token)
 {
  if(!SyncRules.ValidSecret(token))throw new UnauthorizedAccessException();
  var hash=SyncRules.Hash(token);return devices.SingleOrDefault(d=>!d.Revoked&&d.TokenHash==hash)??throw new UnauthorizedAccessException("Gerät nicht freigegeben oder widerrufen.");
 }
 public void StopPairing(){lock(gate){inviteHash=null;pending.Clear();}}
 void Save(List<DeviceRecord> next)
 {
  var temp=path+".tmp";using(var f=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None)){JsonSerializer.Serialize(f,next);f.Flush(true);}File.Move(temp,path,true);
 }
}
