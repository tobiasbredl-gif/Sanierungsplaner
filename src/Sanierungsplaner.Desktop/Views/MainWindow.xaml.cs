using System.Windows;
using Sanierungsplaner.Desktop.ViewModels;
using Sanierungsplaner.Desktop.Services;

namespace Sanierungsplaner.Desktop.Views;

public partial class MainWindow : Window
{
    private async void RefreshProject(object sender, RoutedEventArgs e)
    {
        var model = (MainWindowViewModel)DataContext;
        if (meshBusy) return;
        if (model.IsDirty)
        {
            MessageBox.Show(this, "Bitte Projektänderungen zuerst speichern oder verwerfen.", "Zuerst speichern");
            return;
        }
        RefreshProjectButton.IsEnabled = false;
        try
        {
            await AutomaticMesh();
            model.RefreshCurrentProject();
            if (syncServer?.Running != true)
                model.SyncStatus = "Lokale Daten aktualisiert. Geräteabgleich unter Handys / WLAN starten.";
        }
        finally { RefreshProjectButton.IsEnabled = true; }
    }

    private void DeleteProject(object sender,RoutedEventArgs e)
    {
        var project=(Models.RenovationProject)((System.Windows.Controls.Button)sender).Tag;
        if(MessageBox.Show(this,$"Bist du sicher, dass du das Projekt „{project.Name}“ für alle Nutzer löschen möchtest? Kosten und Zahlungen verschwinden beim nächsten Abgleich auch auf allen Handys. Eine Sicherung bleibt erhalten.","Projekt löschen",MessageBoxButton.YesNo,MessageBoxImage.Warning,MessageBoxResult.No)!=MessageBoxResult.Yes)return;
        try{var model=(MainWindowViewModel)DataContext;new JsonProjectStore(model.StoragePath).Delete(project.Id,project.Revision);model.ReloadCommand.Execute(null);}
        catch(Exception error){MessageBox.Show(this,error.Message,"Löschen nicht abgeschlossen");}
    }
    private readonly System.Windows.Threading.DispatcherTimer meshTimer=new(){Interval=TimeSpan.FromSeconds(60)};
    private bool meshBusy;
    private async Task AutomaticMesh()
    {
        if(meshBusy||syncServer?.Running!=true)return;
        meshBusy=true;
        try
        {
            syncServer.Mesh.Refresh(syncServer.Url);
            await syncServer.Mesh.Engine.Synchronize();
            var model=(MainWindowViewModel)DataContext;
            model.SyncStatus=syncServer.Mesh.Engine.LastStatus;
            if(!model.IsDirty)model.RefreshCurrentProject();
        }
        catch(Exception error){((MainWindowViewModel)DataContext).SyncStatus="Abgleich nicht abgeschlossen: "+error.Message;}
        finally{meshBusy=false;}
    }
    private Sync.LanSyncServer? syncServer;
    private void OpenSync(object sender, RoutedEventArgs e)
    {
        try
        {
            var model=(MainWindowViewModel)DataContext;
            syncServer ??=new Sync.LanSyncServer(new JsonProjectStore(model.StoragePath),System.IO.Path.Combine(System.IO.Path.GetDirectoryName(model.StoragePath)!,"Sync"));
            new SyncWindow(syncServer){Owner=this}.ShowDialog();
        }
        catch(Exception error){MessageBox.Show(this,error.Message,"WLAN-Abgleich");}
    }
    public MainWindow() : this(new MainWindowViewModel(JsonProjectStore.CreateDefault(), new UnsavedChangesPrompt())) { }

    public MainWindow(MainWindowViewModel model)
    {
        InitializeComponent();
        DataContext = model;
        model.ProjectOpened += async (_, _) =>
        {
            if (meshBusy)
            {
                // The running sync will refresh the clean editor when it finishes.
                return;
            }
            await AutomaticMesh();
            model.RefreshCurrentProject();
        };
        meshTimer.Tick+=async(_,_)=>await AutomaticMesh();
        Loaded+=async(_,_)=>
        {
            try
            {
                var folder=System.IO.Path.Combine(System.IO.Path.GetDirectoryName(model.StoragePath)!,"Sync");
                var saved=System.IO.Path.Combine(folder,"auto-address.txt");
                if(System.IO.File.Exists(saved))
                {
                    var address=Sync.LanSyncServer.Addresses().FirstOrDefault(a=>a.Address==System.IO.File.ReadAllText(saved));
                    if(address!=null){syncServer??=new(new JsonProjectStore(model.StoragePath),folder);await syncServer.Start(address);await AutomaticMesh();}
                }
            }
            catch(Exception error){MessageBox.Show(this,"Automatischer Geräteabgleich nicht gestartet: "+error.Message,"WLAN-Abgleich");}
            meshTimer.Start();
        };
        Closed += async (_, _) => { meshTimer.Stop();if(syncServer is not null) await syncServer.DisposeAsync(); };
        Closing += (_, e) => e.Cancel = !model.CanLeaveEditor();
    }
}
