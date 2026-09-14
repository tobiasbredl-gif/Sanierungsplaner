using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Sanierungsplaner.Desktop.Services;
using Sanierungsplaner.Desktop.Sync;
namespace Sanierungsplaner.Sync;

// A deliberately small HTTP/1.1 endpoint: one bounded request per TLS connection,
// no proxying, chunking, redirects, compression or pipelining. Clients send Content-Length.
public sealed class PeerTlsServer : IAsyncDisposable
{

 readonly X509Certificate2 certificate;
 readonly IPAddress address;
 readonly int prefix;
 readonly CancellationTokenSource stop=new();
 readonly SemaphoreSlim slots=new(16);
 readonly System.Collections.Concurrent.ConcurrentDictionary<long,Task> clients=new();
 readonly TcpListener listener;
 Task? accepting;long sequence;

 readonly MeshEngine mesh;
 public string Url=>$"https://{address}:{SyncRules.Port}/";
 public string Fingerprint=>Convert.ToHexString(SHA256.HashData(certificate.RawData));
 public PeerTlsServer(X509Certificate2 certificate,IPAddress address,int prefix,MeshEngine mesh)
 {
  if(!SyncRules.PrivateIp(address)||prefix<8||prefix>32)throw new InvalidDataException("Bitte mit dem privaten Heim-WLAN verbinden.");
  this.certificate=certificate;this.address=address;this.prefix=prefix;this.mesh=mesh;listener=new(address,SyncRules.Port);
 }
 public void Start(){listener.Start(16);accepting=Accept();}
 async Task Accept()
 {
  try
  {
   while(!stop.IsCancellationRequested)
   {
    var socket=await listener.AcceptTcpClientAsync(stop.Token);
    if(socket.Client.RemoteEndPoint is not IPEndPoint peer||!SyncRules.SameSubnet(peer.Address,address,prefix)||!slots.Wait(0)){socket.Dispose();continue;}
    var id=Interlocked.Increment(ref sequence);var task=Serve(socket);clients[id]=task;
    _=task.ContinueWith(_=>{clients.TryRemove(id,out var ignored);slots.Release();},TaskScheduler.Default);
   }
  }
  catch(OperationCanceledException){}catch(SocketException)when(stop.IsCancellationRequested){}
 }
 async Task Serve(TcpClient socket)
 {
  using(socket)
  using(var deadline=CancellationTokenSource.CreateLinkedTokenSource(stop.Token))
  using(var tls=new SslStream(socket.GetStream(),false))
  {
   deadline.CancelAfter(TimeSpan.FromSeconds(20));var ct=deadline.Token;
   try
   {
    await tls.AuthenticateAsServerAsync(new SslServerAuthenticationOptions{ServerCertificate=certificate,EnabledSslProtocols=SslProtocols.Tls12|SslProtocols.Tls13,ApplicationProtocols=[SslApplicationProtocol.Http11]},ct);
    int status;object result;
    try
    {
     var request=await Read(tls,ct);
     (status,result)=Dispatch(request);
    }
    catch(UnauthorizedAccessException){status=401;result=new{Error="Gerät oder Aktion nicht freigegeben."};}
    catch(IOException){status=409;result=new{Error="Projekt geändert. Bitte erneut abgleichen."};}
    catch(Exception e)when(e is InvalidDataException or JsonException or FormatException or OverflowException or ArgumentException){status=400;result=new{Error="Ungültige Anfrage."};}
    var bytes=JsonSerializer.SerializeToUtf8Bytes(result);
    if(bytes.Length>32*1024*1024){status=413;bytes=Encoding.UTF8.GetBytes("{}");}
    var header=Encoding.ASCII.GetBytes($"HTTP/1.1 {status} Result\r\nContent-Type: application/json\r\nContent-Length: {bytes.Length}\r\nCache-Control: no-store\r\nConnection: close\r\n\r\n");
    await tls.WriteAsync(header,ct);await tls.WriteAsync(bytes,ct);await tls.FlushAsync(ct);
   }
   catch(Exception e)when(e is IOException or AuthenticationException or OperationCanceledException or SocketException){}
  }
 }
 sealed record Request(string Method,string Path,string Token,byte[] Body);
 static async Task<Request> Read(Stream stream,CancellationToken ct)
 {
  var header=new List<byte>();var one=new byte[1];
  while(header.Count<16384)
  {
   if(await stream.ReadAsync(one,ct)!=1)throw new InvalidDataException();header.Add(one[0]);
   var n=header.Count;if(n>=4&&header[n-4]==13&&header[n-3]==10&&header[n-2]==13&&header[n-1]==10)break;
  }
  if(header.Count>=16384)throw new InvalidDataException();
  var lines=Encoding.ASCII.GetString(header.ToArray()).Split("\r\n");var first=lines[0].Split(' ');
  if(first.Length!=3||first[2]!="HTTP/1.1")throw new InvalidDataException();
  var headers=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
  foreach(var line in lines.Skip(1).Where(l=>l.Length>0))
  {var colon=line.IndexOf(':');if(colon<=0||!headers.TryAdd(line[..colon],line[(colon+1)..].Trim()))throw new InvalidDataException();}
  if(headers.ContainsKey("Transfer-Encoding")||headers.ContainsKey("Expect"))throw new InvalidDataException();
  var length=headers.TryGetValue("Content-Length",out var size)?int.Parse(size,System.Globalization.CultureInfo.InvariantCulture):0;
  if(length<0||length>SyncRules.MaxBodyBytes)throw new InvalidDataException();
  var bytes=new byte[length];await stream.ReadExactlyAsync(bytes,ct);
  var auth=headers.GetValueOrDefault("Authorization","");
  return new(first[0],first[1],auth.StartsWith("Bearer ",StringComparison.Ordinal)?auth[7..]:"",bytes);
 }
 (int,object) Dispatch(Request request)
 {
  if(request.Method!="POST"||request.Path!="/mesh")return(404,new{});
  var packet=JsonSerializer.Deserialize<SignedPacket>(request.Body,new JsonSerializerOptions(JsonSerializerDefaults.Web))??throw new InvalidDataException();
  return(200,mesh.Exchange(packet));
 }
 public async ValueTask DisposeAsync()
 {
  stop.Cancel();listener.Stop();
  if(accepting!=null)await accepting;
  await Task.WhenAll(clients.Values);stop.Dispose();
 }
}
