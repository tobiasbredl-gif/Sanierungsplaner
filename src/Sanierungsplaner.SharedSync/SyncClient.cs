using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Sanierungsplaner.Desktop.Models;
using Sanierungsplaner.Desktop.Services;
namespace Sanierungsplaner.Sync;
public enum ConflictChoice { Cancel, Pc, Phone }
public sealed class SyncClient : IDisposable
{
 readonly HttpClient http;
 public SyncClient(ClientBinding binding)
 {
  SyncRules.ValidateInvite(new(binding.Url,binding.Fingerprint,binding.Token));
  var handler=new HttpClientHandler{AllowAutoRedirect=false,UseProxy=false};
  handler.ServerCertificateCustomValidationCallback=(_,cert,_,_)=>cert!=null&&DateTime.Now>=cert.NotBefore&&DateTime.Now<=cert.NotAfter&&Convert.ToHexString(SHA256.HashData(cert.RawData)).Equals(binding.Fingerprint,StringComparison.OrdinalIgnoreCase);
  http=new(handler){BaseAddress=new Uri(binding.Url),Timeout=TimeSpan.FromSeconds(12),MaxResponseContentBufferSize=32*1024*1024};
  http.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",binding.Token);
 }
 public static PairInvite Decode(string code)
 {
  if(code.Length>4096||!code.Trim().StartsWith("SP1:",StringComparison.Ordinal))throw new InvalidDataException("Bitte den vollständigen Kopplungscode vom PC einfügen.");
  try{var invite=JsonSerializer.Deserialize<PairInvite>(Convert.FromBase64String(code.Trim()[4..]))??throw new InvalidDataException("Leerer Code.");SyncRules.ValidateInvite(invite);return invite;}catch(FormatException){throw new InvalidDataException("Kopplungscode ungültig.");}
 }
 public async Task Claim(string secret,string name)
 {
  using var response=await http.PostAsJsonAsync("pair",new PairRequest(secret,name,http.DefaultRequestHeaders.Authorization!.Parameter!));await Check(response);
 }
 public async Task<DeviceIdentity> Identity()
 {
  using var r=await http.GetAsync("identity");await Check(r);var identity=await r.Content.ReadFromJsonAsync<DeviceIdentity>()??throw new InvalidDataException("Leere Gerätefreigabe.");
  if(identity.Id==Guid.Empty||!SalesCredit.Recipients.Contains(identity.Role))throw new InvalidDataException("Ungültige Gerätefreigabe.");return identity;
 }
 public async Task Synchronize(JsonProjectStore local,string stateFolder,Func<RenovationProject,RenovationProject,Task<ConflictChoice>> conflict)
 {
  await Identity();Directory.CreateDirectory(stateFolder);
  var path=Path.Combine(stateFolder,"baseline.json");
  var baseline=File.Exists(path)?JsonSerializer.Deserialize<List<SyncBaseline>>(File.ReadAllText(path))??[]:[];
  using var response=await http.GetAsync("projects");await Check(response);
  var remote=(await response.Content.ReadFromJsonAsync<RenovationProject[]>())??throw new InvalidDataException("Leere Projektliste.");
  foreach(var r in remote)r.Validate();
  using var deletedResponse=await http.GetAsync("deleted-projects");await Check(deletedResponse);
  var deleted=await deletedResponse.Content.ReadFromJsonAsync<Guid[]>()??throw new InvalidDataException("Leere Löschliste.");
  foreach(var id in deleted)local.Delete(id,null,true);
  var remoteById=remote.Where(p=>!deleted.Contains(p.Id)).ToDictionary(p=>p.Id);var locals=local.Load();
  foreach(var item in locals)
  {
   remoteById.Remove(item.Id,out var pc);var previous=baseline.SingleOrDefault(b=>b.ProjectId==item.Id);
   var localChanged=previous==null||previous.LocalRevision!=item.Revision;
   var remoteChanged=pc!=null&&(previous==null||previous.RemoteRevision!=pc.Revision);
   if(pc!=null&&Equivalent(item,pc)){Accept(pc,item);continue;}
   if(!localChanged&&pc!=null){Accept(pc,item);continue;}
   if(localChanged&&remoteChanged)
   {
    var choice=await conflict(item,pc!);if(choice==ConflictChoice.Cancel)throw new InvalidDataException("Abgleich wegen Konflikt angehalten. Beide Versionen bleiben erhalten: "+item.Name);
    var backup=Path.Combine(stateFolder,"Konfliktsicherungen");Directory.CreateDirectory(backup);
    File.WriteAllText(Path.Combine(backup,$"{item.Id}-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}-Handy.json"),JsonSerializer.Serialize(item));
    File.WriteAllText(Path.Combine(backup,$"{pc!.Id}-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}-PC.json"),JsonSerializer.Serialize(pc));
    if(choice==ConflictChoice.Pc){Accept(pc,item);continue;}
   }
   var upload=new UploadProject(item,pc?.Revision);var bytes=JsonSerializer.SerializeToUtf8Bytes(upload);if(bytes.Length>SyncRules.MaxBodyBytes)throw new InvalidDataException("Projekt zu groß für den Abgleich (maximal 8 MB): "+item.Name);
   using var content=new ByteArrayContent(bytes);content.Headers.ContentType=new MediaTypeHeaderValue("application/json");
   using var pushed=await http.PostAsync("projects",content);await Check(pushed);
   var receipt=await pushed.Content.ReadFromJsonAsync<SyncReceipt>()??throw new InvalidDataException("Leere Speicherbestätigung.");
   if(receipt.Project.Id!=item.Id||!Equivalent(receipt.Project,item))throw new InvalidDataException("Ungültige Speicherbestätigung.");Accept(receipt.Project,item);
  }
  foreach(var pc in remoteById.Values)Accept(pc,null);
  void Accept(RenovationProject pc,RenovationProject? old)
  {
   pc.Validate();if(old==null||old.Revision!=pc.Revision)local.ReplaceFromSync(pc,old?.Revision);
   baseline.RemoveAll(b=>b.ProjectId==pc.Id);baseline.Add(new(pc.Id,pc.Revision,pc.Revision));
   var temp=path+".tmp";using(var f=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None)){JsonSerializer.Serialize(f,baseline);f.Flush(true);}File.Move(temp,path,true);
  }
 }
 static bool Equivalent(RenovationProject a,RenovationProject b)=>JsonSerializer.Serialize(a with{Revision=Guid.Empty,UpdatedAt=default})==JsonSerializer.Serialize(b with{Revision=Guid.Empty,UpdatedAt=default});
 static async Task Check(HttpResponseMessage response)
 {
  if(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)throw new UnauthorizedAccessException("Gerät noch nicht freigegeben oder Zugriff widerrufen. Bitte am PC prüfen.");
  if(response.StatusCode==HttpStatusCode.Conflict)throw new IOException("Projekt inzwischen geändert. Bitte Abgleich erneut starten; keine Daten wurden überschrieben.");
  if(!response.IsSuccessStatusCode){var text=await response.Content.ReadAsStringAsync();throw new InvalidDataException("Abgleich abgewiesen: "+(text.Length>500?text[..500]:text));}
 }
 public async Task Delete(RenovationProject project)
 {
  if((await Identity()).Role!="Tobias")throw new UnauthorizedAccessException("Nur Tobias darf Projekte löschen.");
  using var projects=await http.GetAsync("projects");await Check(projects);
  var current=(await projects.Content.ReadFromJsonAsync<RenovationProject[]>())?.SingleOrDefault(p=>p.Id==project.Id);
  if(current!=null&&!Equivalent(current,project))throw new IOException("Die Projektstände unterscheiden sich. Bitte zuerst abgleichen und die Löschung erneut prüfen.");
  using var response=await http.PostAsJsonAsync("delete-project",new DeleteProject(project.Id,current?.Revision));await Check(response);
 }
 public void Dispose()=>http.Dispose();
}
