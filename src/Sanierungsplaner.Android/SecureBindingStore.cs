using System.Text;
using System.Text.Json;
using global::Android.Security.Keystore;
using Java.Security;
using Javax.Crypto;
using Javax.Crypto.Spec;
using Sanierungsplaner.Sync;
namespace Sanierungsplaner.AndroidApp;
public sealed class SecureBindingStore(string folder)
{
 const string Alias="sanierungsplaner.device.binding.v1";
 sealed record Envelope(string Iv,string Data);
 string PathName=>System.IO.Path.Combine(folder,"device-binding.encrypted");
 Java.Security.IKey Key()
 {
  using var ks=KeyStore.GetInstance("AndroidKeyStore")!;ks.Load(null);
  if(ks.ContainsAlias(Alias))return ks.GetKey(Alias,null)!;
  using var generator=KeyGenerator.GetInstance(KeyProperties.KeyAlgorithmAes,"AndroidKeyStore")!;
  using var spec=new KeyGenParameterSpec.Builder(Alias,KeyStorePurpose.Encrypt|KeyStorePurpose.Decrypt).SetBlockModes(KeyProperties.BlockModeGcm)!.SetEncryptionPaddings(KeyProperties.EncryptionPaddingNone)!.SetRandomizedEncryptionRequired(true)!.Build();
  generator.Init(spec);return generator.GenerateKey()!;
 }
 public ClientBinding? Load()
 {
  if(!File.Exists(PathName))return null;
  var envelope=JsonSerializer.Deserialize<Envelope>(File.ReadAllText(PathName))??throw new InvalidDataException("Gerätefreigabe beschädigt.");
  using var cipher=Cipher.GetInstance("AES/GCM/NoPadding")!;using var key=Key();using var spec=new GCMParameterSpec(128,Convert.FromBase64String(envelope.Iv));cipher.Init(CipherMode.DecryptMode,key,spec);
  var bytes=cipher.DoFinal(Convert.FromBase64String(envelope.Data))!;
  return JsonSerializer.Deserialize<ClientBinding>(bytes)??throw new InvalidDataException("Gerätefreigabe beschädigt.");
 }
 public void Save(ClientBinding binding)
 {
  using var cipher=Cipher.GetInstance("AES/GCM/NoPadding")!;using var key=Key();cipher.Init(CipherMode.EncryptMode,key);
  var data=cipher.DoFinal(JsonSerializer.SerializeToUtf8Bytes(binding))!;
  var serialized=JsonSerializer.Serialize(new Envelope(Convert.ToBase64String(cipher.GetIV()!),Convert.ToBase64String(data)));
  var temp=PathName+".tmp";File.WriteAllText(temp,serialized);File.Move(temp,PathName,true);
 }
 public void Clear(){if(File.Exists(PathName))File.Delete(PathName);}
}
