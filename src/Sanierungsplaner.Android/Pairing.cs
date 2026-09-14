using global::Android.App;
using global::Android.Content;
using global::Android.OS;
using global::Android.Widget;
using global::Android.Hardware.Biometrics;
using Sanierungsplaner.Sync;
using Sanierungsplaner.Desktop.Services;
using Sanierungsplaner.Desktop.Models;
namespace Sanierungsplaner.AndroidApp;
public partial class MainActivity
{
 SecureBindingStore bindingStore=null!;ClientBinding? binding;bool syncBusy;bool formOpen;
 string syncMessage="Noch nicht mit dem PC gekoppelt.";
 readonly SemaphoreSlim syncGate=new(1,1);
 bool IsTobias=>binding?.Role=="Tobias";
 string SyncFolder=>System.IO.Path.Combine(FilesDir!.AbsolutePath,"Sync");
 EditText? pairingCodeField;
 TaskCompletionSource<bool>? credentialResult;
 global::Android.OS.CancellationSignal? biometricCancellation;
 void LoadIdentity()
 {
  bindingStore=new(FilesDir!.AbsolutePath);
  try{binding=bindingStore.Load();syncMessage=binding?.Role is string role?"Freigegeben als "+role:"Noch keine PC-Freigabe.";}catch{binding=null;syncMessage="Gerätefreigabe nicht lesbar. Bitte neu koppeln.";}
  if(File.Exists(System.IO.Path.Combine(SyncFolder,"last-sync.txt")))syncMessage+=" · "+File.ReadAllText(System.IO.Path.Combine(SyncFolder,"last-sync.txt"));
  repayments.IsAuthorized=()=>IsTobias;
 }
 void SyncOptions(LinearLayout box)
 {
  Text(box,"PC-Kopplung und Gerätefreigabe",21,true);
  Text(box,"Der PC verwaltet eure Freigaben. Die Handys gleichen beim App-Start und nachts zwischen 22 und 03 Uhr direkt im Heim-WLAN ab.");
  Text(box,MeshRuntime.Status,13);
  Button(box,"Hintergrundabgleich einschalten",()=>{StartMeshService(true);return Task.CompletedTask;});
  Button(box,"Hintergrundabgleich ausschalten",async()=>{GetSharedPreferences("sync",FileCreationMode.Private)!.Edit()!.PutBoolean("background",false)!.Apply();StopService(new Intent(this,typeof(MeshService)));await MeshRuntime.Stop();});
  Text(box,binding?.Role is string role?"Deine vom PC zugewiesene Rolle: "+role:"Keine aktive Rolle. Erstattungen sind gesperrt.");
  Text(box,syncMessage,13);
  if(binding!=null)Text(box,"Prüfnummer für den PC: "+SyncRules.Hash(binding.Token)[..8],18,true);
  Text(box,"Am PC Handys / WLAN öffnen, Heimnetz starten und einen Code mit der gewünschten Rolle erzeugen. Diesen Code hier einfügen. Danach das Handy am PC freigeben.");
  Button(box,binding==null?"PC koppeln":"Neu koppeln",Pair);
  Button(box,"Freigabe prüfen / Jetzt abgleichen",()=>Synchronize(false),binding!=null);
  if(binding!=null)Button(box,"Verbindung auf diesem Handy entfernen",async()=>{if(await Confirm("Verbindung entfernen?","Lokale Projekte bleiben erhalten. Am PC das Gerät zusätzlich widerrufen.")){StopService(new Intent(this,typeof(MeshService)));await MeshRuntime.Stop();bindingStore.Clear();binding=null;syncMessage="Verbindung entfernt.";Render();}});
 }
 void StartMeshService(bool enable=false)
 {
  var preferences=GetSharedPreferences("sync",FileCreationMode.Private)!;
  if(enable)preferences.Edit()!.PutBoolean("background",true)!.Apply();
  if(!preferences.GetBoolean("background",true))return;
  if(OperatingSystem.IsAndroidVersionAtLeast(33)&&CheckSelfPermission("android.permission.POST_NOTIFICATIONS")!=global::Android.Content.PM.Permission.Granted)RequestPermissions(new[]{"android.permission.POST_NOTIFICATIONS"},714);
  StartForegroundService(new Intent(this,typeof(MeshService)));
 }
 async Task Pair()
 {
  if(binding!=null&&!await Confirm("Neu koppeln?","Die bisherige Verbindung auf dem Handy wird ersetzt. Am PC alte Gerätefreigaben bei Bedarf widerrufen. Lokale Projekte bleiben erhalten."))return;
  string code="";string name=global::Android.OS.Build.Model??"Android-Handy";
  var entered=await Form("Mit PC koppeln",box=>{pairingCodeField=Field(box,"Kopplungscode vom PC",code,v=>code=v,max:4096,multiline:true);Button(box,"Kopplungsdatei öffnen",()=>{var intent=new Intent(Intent.ActionOpenDocument);intent.SetType("text/*");intent.AddCategory(Intent.CategoryOpenable);StartActivityForResult(intent,713);return Task.CompletedTask;});Field(box,"Name dieses Handys",name,v=>name=v,max:80);},()=>code.Length>0&&!string.IsNullOrWhiteSpace(name)?code:null,()=>"Bitte Code und Gerätenamen eingeben.");
  if(entered==null)return;
  var invite=SyncClient.Decode(entered);var candidate=new ClientBinding(invite.Url,invite.Fingerprint,SyncRules.NewSecret(),null,null);
  using(var client=new SyncClient(candidate))await client.Claim(invite.Secret,name);
  StopService(new Intent(this,typeof(MeshService)));await MeshRuntime.Stop();
  var meshFolder=System.IO.Path.Combine(FilesDir!.AbsolutePath,"Mesh");
  var anchor=System.IO.Path.Combine(meshFolder,"authority.cer");
  if(File.Exists(anchor))
  {
   using var old=System.Security.Cryptography.X509Certificates.X509CertificateLoader.LoadCertificate(Convert.FromBase64String(File.ReadAllText(anchor)));
   if(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(old.RawData))!=candidate.Fingerprint)Directory.Move(meshFolder,meshFolder+"-Sicherung-"+DateTime.UtcNow.Ticks);
  }
  bindingStore.Save(candidate);binding=candidate;syncMessage="Anfrage gesendet. Prüfnummer am PC vergleichen: "+SyncRules.Hash(candidate.Token)[..8]+". Dieses Handy am PC freigeben, danach Freigabe prüfen wählen.";Render();await Message("Freigabe am PC",syncMessage);
 }
 async Task Synchronize(bool automatic)
 {
  if(binding==null||syncBusy||formOpen)return;
  if(automatic&&model.IsDirty)return;
  if(model.IsDirty){if(automatic)return;await Message("Zuerst speichern","Bitte Projektänderungen speichern oder verwerfen, bevor du abgleichst.");return;}
  await syncGate.WaitAsync();syncBusy=true;
  try
  {
   var selected=model.SelectedProjectTab;var currentId=openedId;var editing=model.IsEditing;
   StartMeshService();
   await MeshRuntime.Tick(this);
   binding=bindingStore.Load();
   syncMessage=MeshRuntime.Status;
   Directory.CreateDirectory(SyncFolder);File.WriteAllText(System.IO.Path.Combine(SyncFolder,"last-sync.txt"),syncMessage);
   if(model.IsDirty)return;
   selected=model.SelectedProjectTab;currentId=openedId;editing=model.IsEditing;
   model.ReloadCommand.Execute(null);
   if(editing&&currentId is Guid id){var p=model.Projects.FirstOrDefault(p=>p.Id==id);if(p!=null){model.OpenProjectCommand.Execute(p);model.SelectedProjectTab=selected;}}
   Render();
  }
  catch(UnauthorizedAccessException)
  {
   if(binding!=null){binding=binding with{Role=null};bindingStore.Save(binding);}syncMessage="Keine Freigabe oder Zugriff widerrufen. Erstattungen bleiben gesperrt.";Render();if(!automatic)await Message("PC-Freigabe prüfen",syncMessage);
  }
  catch(Exception ex){syncMessage="Abgleich nicht abgeschlossen: "+ex.Message;if(!automatic){Render();await Message("WLAN-Abgleich",syncMessage);}}
  finally{syncBusy=false;syncGate.Release();}
 }
 async Task DeleteForEveryone(RenovationProject project)
 {
  if(!IsTobias||binding==null||syncBusy)return;
  formOpen=true;
  try
  {
   if(!await Confirm("Projekt für alle löschen?",$"Bist du sicher, dass du das Projekt „{project.Name}“ für alle Nutzer löschen möchtest? Kosten und Zahlungen werden beim nächsten Abgleich auf allen Geräten entfernt. Eine Sicherung bleibt erhalten.","Für alle löschen"))return;
   if(!await ConfirmOwner("Projektlöschung als Tobias bestätigen"))return;
   await syncGate.WaitAsync();syncBusy=true;
   try
   {
    await MeshRuntime.Gate.WaitAsync();
    try{if(MeshRuntime.Engine==null)throw new InvalidOperationException("Zuerst Geräteabgleich einrichten.");MeshRuntime.Engine.Delete(project);}finally{MeshRuntime.Gate.Release();}
    model.ReloadCommand.Execute(null);Render();
    await Message("Projekt gelöscht","Die Löschung ist gespeichert. PC und andere Handys übernehmen sie beim nächsten Abgleich.");
   }
   finally{syncBusy=false;syncGate.Release();}
  }
  finally{formOpen=false;}
 }
 async Task<bool> ConfirmOwner(string title="Erstattung als Tobias bestätigen")
 {
  if(!IsTobias){await Message("Nur Tobias","Dieses Handy ist nicht als Tobias freigegeben.");return false;}
  var keyguard=(KeyguardManager)GetSystemService(KeyguardService)!;
  if(!keyguard.IsDeviceSecure){await Message("Handysperre erforderlich","Bitte zuerst eine sichere Geräte-PIN, ein Passwort oder ein Entsperrmuster in Android einrichten.");return false;}
  if(credentialResult!=null)return false;
  credentialResult=new(TaskCreationOptions.RunContinuationsAsynchronously);
  try
  {
   if(OperatingSystem.IsAndroidVersionAtLeast(30))
   {
    biometricCancellation=new();
    var p=new BiometricPrompt.Builder(this).SetTitle(title)!.SetSubtitle("Fingerabdruck oder Geräte-PIN")!.SetAllowedAuthenticators((int)(BiometricManagerAuthenticators.BiometricStrong|BiometricManagerAuthenticators.DeviceCredential))!.Build();
    p.Authenticate(biometricCancellation,MainExecutor!,new AuthResult(ok=>credentialResult?.TrySetResult(ok)));
   }
   else
   {
#pragma warning disable CS0618, CA1422
    var intent=keyguard.CreateConfirmDeviceCredentialIntent(title,"Gerätesperre für Tobias bestätigen");
#pragma warning restore CS0618, CA1422
    if(intent==null)return false;StartActivityForResult(intent,712);
   }
   return await credentialResult.Task&&IsTobias;
  }
  finally{credentialResult=null;biometricCancellation?.Dispose();biometricCancellation=null;}
 }
 protected override void OnActivityResult(int requestCode,Result resultCode,Intent? data)
  {
  base.OnActivityResult(requestCode,resultCode,data);
  if(requestCode==712)credentialResult?.TrySetResult(resultCode==Result.Ok);
  if(requestCode==713&&resultCode==Result.Ok&&data?.Data!=null&&pairingCodeField!=null)
  {
   try{using var stream=ContentResolver!.OpenInputStream(data.Data)!;using var reader=new StreamReader(stream);var buffer=new char[4097];var count=reader.ReadBlock(buffer,0,buffer.Length);if(count>4096)throw new InvalidDataException("Kopplungsdatei zu groß.");pairingCodeField.Text=new string(buffer,0,count);}
   catch(Exception ex){Run(()=>Message("Kopplungsdatei",ex.Message));}
  }
 }
 [System.Runtime.Versioning.SupportedOSPlatform("android28.0")]
 sealed class AuthResult(Action<bool> result):BiometricPrompt.AuthenticationCallback
 {
  public override void OnAuthenticationSucceeded(BiometricPrompt.AuthenticationResult? value){base.OnAuthenticationSucceeded(value);result(true);}
  public override void OnAuthenticationError([global::Android.Runtime.GeneratedEnum] BiometricErrorCode errorCode,Java.Lang.ICharSequence? error){base.OnAuthenticationError(errorCode,error);result(false);}
 }
}
