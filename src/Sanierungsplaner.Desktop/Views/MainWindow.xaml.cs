using System.Windows;
using Sanierungsplaner.Desktop.ViewModels;
using Sanierungsplaner.Desktop.Services;

namespace Sanierungsplaner.Desktop.Views;

public partial class MainWindow : Window
{
    private void DeleteProject(object sender,RoutedEventArgs e)
    {
        var project=(Models.RenovationProject)((System.Windows.Controls.Button)sender).Tag;
        if(MessageBox.Show(this,$"„{project.Name}“ für alle löschen? Kosten und Zahlungen verschwinden beim nächsten Abgleich auch auf allen Handys. Eine Sicherung bleibt im Ordner Projects/Deleted erhalten.","Projekt löschen",MessageBoxButton.YesNo,MessageBoxImage.Warning,MessageBoxResult.No)!=MessageBoxResult.Yes)return;
        try{var model=(MainWindowViewModel)DataContext;new JsonProjectStore(model.StoragePath).Delete(project.Id,project.Revision);model.ReloadCommand.Execute(null);}
        catch(Exception error){MessageBox.Show(this,error.Message,"Löschen nicht abgeschlossen");}
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
        Closed += async (_, _) => { if(syncServer is not null) await syncServer.DisposeAsync(); };
        Closing += (_, e) => e.Cancel = !model.CanLeaveEditor();
    }
}
