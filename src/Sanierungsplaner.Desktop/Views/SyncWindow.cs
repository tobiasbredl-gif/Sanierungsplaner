using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Sanierungsplaner.Desktop.Sync;
using Sanierungsplaner.Desktop.Models;
namespace Sanierungsplaner.Desktop.Views;
public sealed class SyncWindow : Window
{
 readonly LanSyncServer server;readonly ComboBox address=new(),role=new();readonly TextBox invitation=new();readonly TextBlock status=new();readonly StackPanel devices=new();readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromSeconds(2)};
 string previous="";
 public SyncWindow(LanSyncServer server)
 {
  this.server=server;Title="Handys und WLAN-Abgleich";Width=720;Height=800;MinWidth=580;MinHeight=500;WindowStartupLocation=WindowStartupLocation.CenterOwner;
  var panel=new StackPanel{Margin=new Thickness(24)};Content=new ScrollViewer{Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
  panel.Children.Add(new TextBlock{Text="Handys sicher freigeben",FontSize=26,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,16)});
  panel.Children.Add(new TextBlock{Text="PC und Handy müssen im selben Heimnetz sein. Erstattungen und deren Stornos dürfen nur Handys mit der Rolle Tobias senden. Nicht freigegebene Geräte erhalten keine Projektdaten.",TextWrapping=TextWrapping.Wrap});
  address.ItemsSource=LanSyncServer.Addresses();address.SelectedIndex=0;address.Margin=new Thickness(0,16,0,8);panel.Children.Add(address);
  Add(panel,"WLAN-Abgleich starten",async()=>{if(address.SelectedItem is not LanSyncServer.LanAddress selected)throw new InvalidOperationException("Keine private Netzwerkadresse gefunden.");await server.Start(selected);status.Text="Bereit: "+server.Url+" · Fenster darf geschlossen werden; die Windows-App muss geöffnet bleiben.";});
  Add(panel,"WLAN-Abgleich stoppen",async()=>{await server.Stop();invitation.Clear();status.Text="WLAN-Abgleich gestoppt.";});
  Add(panel,"Windows-Zugriff im privaten Heimnetz erlauben",()=>
  {
   var exe=Environment.ProcessPath??throw new InvalidOperationException("Programmpfad fehlt.");
   var escaped=exe.Replace("'","''");
   var script=$"New-NetFirewallRule -DisplayName 'Sanierungsplaner Heimnetz' -Direction Inbound -Action Allow -Protocol TCP -LocalPort 58443 -RemoteAddress LocalSubnet -Profile Private -Program '{escaped}'";
   Process.Start(new ProcessStartInfo("powershell.exe","-NoProfile -EncodedCommand "+Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(script))){UseShellExecute=true,Verb="runas",WindowStyle=ProcessWindowStyle.Hidden});
   status.Text="Windows fragt nach Administratorfreigabe. Die Regel gilt nur im privaten Netzwerk und lokalen Subnetz.";return Task.CompletedTask;
  });
  status.TextWrapping=TextWrapping.Wrap;status.Margin=new Thickness(0,12,0,12);panel.Children.Add(status);
  panel.Children.Add(new TextBlock{Text="1. Rolle für dieses Handy am PC wählen",FontWeight=FontWeights.SemiBold});role.ItemsSource=SalesCredit.Recipients;role.SelectedIndex=0;role.Margin=new Thickness(0,8,0,8);panel.Children.Add(role);
  Add(panel,"2. Einmaligen Kopplungscode erzeugen",()=>{invitation.Text=server.Invite((string)role.SelectedItem);status.Text="Code 5 Minuten gültig, nur einmal verwendbar. Auf dem Handy unter PC koppeln einfügen. Anschließend das Gerät hier freigeben.";return Task.CompletedTask;});
  invitation.IsReadOnly=true;invitation.TextWrapping=TextWrapping.Wrap;invitation.Height=110;invitation.VerticalScrollBarVisibility=ScrollBarVisibility.Auto;panel.Children.Add(invitation);
  Add(panel,"Kopplungscode kopieren",()=>{if(invitation.Text.Length>0)Clipboard.SetText(invitation.Text);return Task.CompletedTask;});
  Add(panel,"Kopplungscode als Datei speichern",()=>
  {
   if(invitation.Text.Length==0)throw new InvalidOperationException("Zuerst einen Code erzeugen.");
   var file=new Microsoft.Win32.SaveFileDialog{FileName="Sanierungsplaner-Kopplung.txt",Filter="Kopplungsdatei|*.txt"};
   if(file.ShowDialog(this)==true)System.IO.File.WriteAllText(file.FileName,invitation.Text);
   return Task.CompletedTask;
  });
  panel.Children.Add(new TextBlock{Text="3. Handy prüfen und freigeben",FontSize=20,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,18,0,12)});panel.Children.Add(devices);
  timer.Tick+=(_,_)=>Refresh();timer.Start();Closed+=(_,_)=>timer.Stop();Refresh();
 }
 void Refresh()
 {
  var pending=server.Registry.Pending;var known=server.Registry.Devices;var key=string.Join(";",pending.Select(p=>p.Id))+string.Join(";",known.Select(d=>d.Id+":"+d.Revoked));if(key==previous&&devices.Children.Count>0)return;previous=key;devices.Children.Clear();
  foreach(var p in pending)
  {
   devices.Children.Add(new TextBlock{Text=p.Name+" · Rolle: "+p.Role+" · Prüfnummer: "+p.TokenHash[..8],TextWrapping=TextWrapping.Wrap});
   Add(devices,"Als "+p.Role+" freigeben",()=>{if(MessageBox.Show(this,$"Ist „{p.Name}“ dein erwartetes Handy? Stimmt die Prüfnummer {p.TokenHash[..8]} auf dem Handy überein? Rolle {p.Role} freigeben?", "Gerät freigeben",MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)==MessageBoxResult.Yes)server.Registry.Approve(p.Id);Refresh();return Task.CompletedTask;});
   Add(devices,"Anfrage ablehnen",()=>{server.Registry.Deny(p.Id);Refresh();return Task.CompletedTask;});
  }
  foreach(var d in known)
  {
   devices.Children.Add(new TextBlock{Text=d.Name+" · "+d.Role+(d.Revoked?" · Widerrufen":" · Freigegeben"),Margin=new Thickness(0,12,0,4),TextWrapping=TextWrapping.Wrap});
   if(!d.Revoked)Add(devices,"Zugriff widerrufen",()=>{if(MessageBox.Show(this,"Zugriff für "+d.Name+" widerrufen? Der nächste Abgleich wird abgewiesen.","Gerät sperren",MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)==MessageBoxResult.Yes)server.Registry.Revoke(d.Id);Refresh();return Task.CompletedTask;});
  }
  if(pending.Count==0&&known.Count==0)devices.Children.Add(new TextBlock{Text="Noch keine Geräte oder Anfragen."});
 }
 void Add(Panel panel,string title,Func<Task> action)
 {
  var button=new Button{Content=title,Margin=new Thickness(0,6,0,6),Padding=new Thickness(10)};panel.Children.Add(button);
  button.Click+=async(_,_)=>{button.IsEnabled=false;try{await action();}catch(Exception ex){status.Text=ex.Message;}finally{button.IsEnabled=true;}};
 }
}
