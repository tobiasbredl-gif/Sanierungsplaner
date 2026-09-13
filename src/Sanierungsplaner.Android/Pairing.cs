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
  Text(box,binding?.Role is string role?"Deine vom PC zugewiesene Rolle: "+role:"Keine aktive Rolle. Erstattungen sind gesperrt.");
  Text(box,syncMessage,13);
  if(binding!=null)Text(box,"Prüfnummer für den PC: "+SyncRules.Hash(binding.Token)[..8],18,true);
  Text(box,"Am PC Handys / WLAN öffnen, Heimnetz starten und einen Code mit der gewünschten Rolle erzeugen. Diesen Code hier einfügen. Danach das Handy am PC freigeben.");
  Button(box,binding==null?"PC koppeln":"Neu koppeln",Pair);
  Button(box,"Freigabe prüfen / Jetzt abgleichen",()=>Synchronize(false),binding!=null);
  if(binding!=null)Button(box,"Verbindung auf diesem Handy entfernen",async()=>{if(await Confirm("Verbindung entfernen?","Lokale Projekte bleiben erhalten. Am PC das Gerät zusätzlich widerrufen.")){bindingStore.Clear();binding=null;syncMessage="Verbindung entfernt.";Render();}});
 }
 async Task Pair()
 {
  if(binding!=null&&!await Confirm("Neu koppeln?","Die bisherige Verbindung auf dem Handy wird ersetzt. Am PC alte Gerätefreigaben bei Bedarf widerrufen. Lokale Projekte bleiben erhalten."))return;
  string code="";string name=global::Android.OS.Build.Model??"Android-Handy";
  var entered=await Form("Mit PC koppeln",box=>{pairingCodeField=Field(box,"Kopplungscode vom PC",code,v=>code=v,max:4096,multiline:true);Button(box,"Kopplungsdatei öffnen",()=>{var intent=new Intent(Intent.ActionOpenDocument);intent.SetType("text/*");intent.AddCategory(Intent.CategoryOpenable);StartActivityForResult(intent,713);return Task.CompletedTask;});Field(box,"Name dieses Handys",name,v=>name=v,max:80);},()=>code.Length>0&&!string.IsNullOrWhiteSpace(name)?code:null,()=>"Bitte Code und Gerätenamen eingeben.");
  if(entered==null)return;
  var invite=SyncClient.Decode(entered);var candidate=new ClientBinding(invite.Url,invite.Fingerprint,SyncRules.NewSecret(),null,null);
  using(var client=new SyncClient(candidate))await client.Claim(invite.Secret,name);
  bindingStore.Save(candidate);binding=candidate;syncMessage="Anfrage gesendet. Prüfnummer am PC vergleichen: "+SyncRules.Hash(candidate.Token)[..8]+". Dieses Handy am PC freigeben, danach Freigabe prüfen wählen.";Render();await Message("Freigabe am PC",syncMessage);
 }
 async Task Synchronize(bool automatic)
 {
  if(binding==null||syncBusy||formOpen)return;
  if(automatic&&(model.IsEditing||model.ShowAbout))return;
  if(model.IsDirty){if(automatic)return;await Message("Zuerst speichern","Bitte Projektänderungen speichern oder verwerfen, bevor du abgleichst.");return;}
  await syncGate.WaitAsync();syncBusy=true;
  try
  {
   using var client=new SyncClient(binding);var identity=await client.Identity();
   binding=binding with{Role=identity.Role,DeviceId=identity.Id};bindingStore.Save(binding);
   var selected=model.SelectedProjectTab;var currentId=openedId;var editing=model.IsEditing;
   using var busy=new AlertDialog.Builder(this)!.SetTitle("Sicherer WLAN-Abgleich")!.SetMessage("Projekte werden geprüft …")!.SetCancelable(false)!.Create()!;if(!automatic)busy.Show();
   try
   {
    await client.Synchronize(new JsonProjectStore(model.StoragePath),SyncFolder,async(phone,pc)=>
    {
     if(automatic)return ConflictChoice.Cancel;
     var choice=await Choose("Konflikt: "+phone.Name,new[]{"PC-Version übernehmen","Handy-Version übertragen","Abbrechen – beide behalten"});
     if(choice==0&&await Confirm("PC-Version übernehmen?","Die lokalen Änderungen werden durch den PC-Stand ersetzt. Beide Stände werden vorher als lokale Konfliktsicherung aufbewahrt."))return ConflictChoice.Pc;
     if(choice==1&&await Confirm("Handy-Version übertragen?","Die Änderungen am PC werden durch den Handy-Stand ersetzt. Bestehende Protokolle dürfen nicht entfernt werden; der PC kann die Übernahme ablehnen."))return ConflictChoice.Phone;
     return ConflictChoice.Cancel;
    });
   }finally{if(!automatic)busy.Dismiss();}
   syncMessage="Letzter erfolgreicher Abgleich: "+DateTime.Now.ToString("dd.MM.yyyy HH:mm");
   File.WriteAllText(System.IO.Path.Combine(SyncFolder,"last-sync.txt"),syncMessage);
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
 async Task<bool> ConfirmOwner()
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
    var p=new BiometricPrompt.Builder(this).SetTitle("Erstattung als Tobias bestätigen")!.SetSubtitle("Fingerabdruck oder Geräte-PIN")!.SetAllowedAuthenticators((int)(BiometricManagerAuthenticators.BiometricStrong|BiometricManagerAuthenticators.DeviceCredential))!.Build();
    p.Authenticate(biometricCancellation,MainExecutor!,new AuthResult(ok=>credentialResult?.TrySetResult(ok)));
   }
   else
   {
#pragma warning disable CS0618, CA1422
    var intent=keyguard.CreateConfirmDeviceCredentialIntent("Erstattung bestätigen","Gerätesperre für Tobias bestätigen");
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
