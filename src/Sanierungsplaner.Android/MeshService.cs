using global::Android.App;
using global::Android.Content;
using global::Android.Content.PM;
using global::Android.OS;
namespace Sanierungsplaner.AndroidApp;
[Service(Exported=false,ForegroundServiceType=ForegroundService.TypeConnectedDevice)]
public sealed class MeshService:Service
{
 CancellationTokenSource? stopping;Task? loop;
 public override IBinder? OnBind(Intent? intent)=>null;
 public override void OnCreate()
 {
  base.OnCreate();
  var manager=(NotificationManager)GetSystemService(NotificationService)!;
  manager.CreateNotificationChannel(new NotificationChannel("local-sync","Lokaler Geräteabgleich",NotificationImportance.Low));
  var launch=new Intent(this,typeof(MainActivity));
  var pending=PendingIntent.GetActivity(this,0,launch,PendingIntentFlags.Immutable|PendingIntentFlags.UpdateCurrent);
  var notification=new Notification.Builder(this,"local-sync").SetContentTitle("Sanierungsplaner · Geräteabgleich")!.SetContentText("Im Heim-WLAN erreichbar. Nachts 22–03 Uhr werden Änderungen abgeglichen.")!.SetSmallIcon(global::Android.Resource.Drawable.IcPopupSync)!.SetContentIntent(pending)!.SetOngoing(true)!.Build();
  StartForeground(120,notification);
 }
 public override StartCommandResult OnStartCommand(Intent? intent,StartCommandFlags flags,int startId)
 {
  if(loop==null){stopping=new();loop=Loop(stopping.Token);}
  return StartCommandResult.Sticky;
 }
 async Task Loop(CancellationToken ct)
 {
  var first=true;
  try
  {
   while(!ct.IsCancellationRequested)
   {
    if(first||Sanierungsplaner.Sync.MeshSchedule.IsNight(DateTime.Now))
    {try{await MeshRuntime.Tick(this);}catch(Exception){/* Visible status is retained; retry on next interval. */}}
    first=false;await Task.Delay(TimeSpan.FromMinutes(10),ct);
   }
  }
  catch(System.OperationCanceledException){}
 }
 public override void OnDestroy(){stopping?.Cancel();_ = MeshRuntime.Stop();base.OnDestroy();}
}
