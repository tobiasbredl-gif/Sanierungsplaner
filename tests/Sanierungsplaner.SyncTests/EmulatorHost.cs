using System.Net.Http.Json;
using System.Security.Cryptography.X509Certificates;
using Sanierungsplaner.Sync;
using System.Security.Cryptography;
using Sanierungsplaner.Desktop.Models;
using Sanierungsplaner.Desktop.Services;
using Sanierungsplaner.Desktop.Sync;
internal static class EmulatorHost
{
 public static async Task Run(string folder)
 {
  Directory.CreateDirectory(folder);var store=new JsonProjectStore(Path.Combine(folder,"Projects"));var now=DateTimeOffset.UtcNow;
  if(store.Load().Count==0)store.Save(new RenovationProject(Guid.NewGuid(),Guid.NewGuid(),"Kopplungstest-PC","","Isolierte Testdaten",now,now){Items=[new CostItem(Guid.NewGuid(),"Testmaterial","","",1,"Stück",4100,"Gekauft",new Payments(Lea:100,Tobias:4000))]},null);
  var security=Path.Combine(folder,"Security");await using var server=new LanSyncServer(store,security);
  await server.Start(LanSyncServer.Addresses().First());
  var role="Tobias";File.WriteAllText(Path.Combine(folder,"invite.txt"),server.Invite(role));Console.WriteLine("Isolated emulator host ready.");
  try
  {
   while(!File.Exists(Path.Combine(folder,"stop.signal")))
   {
    foreach(var p in server.Registry.Pending.Where(p=>p.Name.StartsWith("Emulator-",StringComparison.Ordinal)))server.Registry.Approve(p.Id);
    var roleFile=Path.Combine(folder,"role.txt");if(File.Exists(roleFile)){var next=File.ReadAllText(roleFile).Trim();if(next!=role){role=next;File.WriteAllText(Path.Combine(folder,"invite.txt"),server.Invite(role));}}
    var revoke=Path.Combine(folder,"revoke.signal");if(File.Exists(revoke)){foreach(var d in server.Registry.Devices.Where(d=>!d.Revoked))server.Registry.Revoke(d.Id);File.Delete(revoke);}
    var peerSignal=Path.Combine(folder,"peer.signal");
    if(File.Exists(peerSignal))
    {
     File.Delete(peerSignal);
     try
     {
      var directory=server.Mesh.Refresh(server.Url);var device=directory.Data.Devices.First(d=>d.Id!=directory.Data.PcId&&!d.Revoked);
      using var cert=X509CertificateLoader.LoadCertificate(Convert.FromBase64String(device.Certificate));
      using var handler=new HttpClientHandler{UseProxy=false,ServerCertificateCustomValidationCallback=(_,c,_,_)=>c!=null&&c.RawData.SequenceEqual(cert.RawData)};
      using var client=new HttpClient(handler){Timeout=TimeSpan.FromSeconds(15)};
      using var body=new ByteArrayContent(MeshEngine.Bytes(server.Mesh.Engine.Packet()));body.Headers.ContentType=new("application/json");
      using var response=await client.PostAsync("https://127.0.0.1:58444/mesh",body);response.EnsureSuccessStatusCode();
      server.Mesh.Engine.Receive(await response.Content.ReadFromJsonAsync<SignedPacket>()??throw new Exception("Empty peer response"));
      File.WriteAllText(Path.Combine(folder,"peer-result.txt"),"PASS: pinned HTTPS request to native Android peer listener and signed response accepted.");
     }
     catch(Exception e){File.WriteAllText(Path.Combine(folder,"peer-result.txt"),e.ToString());}
    }
    await Task.Delay(300);
   }
  }
  finally{await server.Stop();var keyName=Path.Combine(security,"server-key-name.txt");if(File.Exists(keyName)){using var key=CngKey.Open(File.ReadAllText(keyName));key.Delete();}}
 }
}
