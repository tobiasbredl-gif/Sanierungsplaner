using System.IO;
using System.Net;
using System.Security.Cryptography;
using Sanierungsplaner.Desktop.Models;
namespace Sanierungsplaner.Sync;
public sealed record PairInvite(string Url, string Fingerprint, string Secret);
public sealed record PairRequest(string Secret, string DeviceName, string Token);
public sealed record DeviceIdentity(Guid Id, string Name, string Role);
public sealed record DeviceRecord(Guid Id, string Name, string Role, string TokenHash, bool Revoked, DateTimeOffset ApprovedAt);
public sealed record UploadProject(RenovationProject Project, Guid? ExpectedRevision);
public sealed record SyncReceipt(RenovationProject Project);
public sealed record ClientBinding(string Url, string Fingerprint, string Token, string? Role, Guid? DeviceId);
public sealed record SyncBaseline(Guid ProjectId, Guid LocalRevision, Guid RemoteRevision);
public static class SyncRules
{
 public const int Port = 58443;
 public const int MaxBodyBytes = 8 * 1024 * 1024;
 public static string NewSecret() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
 public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)));
 public static bool PrivateIp(IPAddress ip)
 {
  var b=ip.MapToIPv4().GetAddressBytes();
  return ip.AddressFamily==System.Net.Sockets.AddressFamily.InterNetwork && (b[0]==10 || b[0]==172 && b[1]>=16 && b[1]<=31 || b[0]==192 && b[1]==168);
 }
 public static bool SameSubnet(IPAddress peer, IPAddress local, int prefix)
 {
  if(peer.IsIPv4MappedToIPv6) peer=peer.MapToIPv4();
  if(!PrivateIp(peer)||!PrivateIp(local)||prefix<8||prefix>32)return false;
  var a=peer.GetAddressBytes();var b=local.GetAddressBytes();
  for(int i=0;i<4;i++){var bits=Math.Clamp(prefix-i*8,0,8);var mask=(byte)(0xff << (8-bits));if((a[i]&mask)!=(b[i]&mask))return false;}
  return true;
 }
 public static void ValidateInvite(PairInvite invite)
 {
  if(!Uri.TryCreate(invite.Url,UriKind.Absolute,out var u)||u.Scheme!="https"||u.Port!=Port||u.AbsolutePath!="/"||u.Query!=""||u.Fragment!=""||u.UserInfo!=""||!IPAddress.TryParse(u.Host,out var ip)||!PrivateIp(ip))throw new InvalidDataException("Bitte den Kopplungscode der Windows-App verwenden. Nur private IPv4-Heimnetz-Adressen sind erlaubt.");
  if(invite.Fingerprint.Length!=64||!invite.Fingerprint.All(Uri.IsHexDigit)||!ValidSecret(invite.Secret))throw new InvalidDataException("Ungültiger Kopplungscode.");
 }
 public static bool ValidSecret(string? value)
 {
  if(value is null||value.Length!=44)return false;
  try{return Convert.FromBase64String(value).Length==32;}catch(FormatException){return false;}
 }
 public static void AuthorizeUpload(string role,RenovationProject? current,RenovationProject proposed)
 {
  if(!SalesCredit.Recipients.Contains(role))throw new UnauthorizedAccessException("Unbekannte Rolle.");
  proposed.Validate();
  if(role!="Tobias"&&!proposed.Reimbursements.SequenceEqual(current?.Reimbursements??[]))throw new UnauthorizedAccessException("Nur Tobias darf Erstattungen und deren Stornos erfassen.");
  // Existing journals cannot be removed or rewritten, including by Tobias.
  if(current!=null && (!proposed.Reimbursements.Take(current.Reimbursements.Length).SequenceEqual(current.Reimbursements)||!proposed.Credits.Take(current.Credits.Length).SequenceEqual(current.Credits)||!proposed.IncomingRepayments.Take(current.IncomingRepayments.Length).SequenceEqual(current.IncomingRepayments)))throw new InvalidDataException("Gespeicherte Protokolle dürfen nur durch weitere Einträge ergänzt werden.");
 }
}
public sealed record DeleteProject(Guid Id, Guid? ExpectedRevision);
