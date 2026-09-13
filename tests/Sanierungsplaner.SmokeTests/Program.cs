using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Sanierungsplaner.Desktop;
using Sanierungsplaner.Desktop.ViewModels;
using Sanierungsplaner.Desktop.Views;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            var app = new App();
            app.InitializeComponent();
            var window = new MainWindow();
            window.Show();
            window.UpdateLayout();
            var model = (MainWindowViewModel)window.DataContext;
            var notifications = new List<string?>();
            model.PropertyChanged += (_, e) => notifications.Add(e.PropertyName);
            var homeTitle = model.PageTitle;
            model.ShowAboutCommand.Execute(null);
            if (!model.ShowAbout || model.PageTitle == homeTitle || !notifications.Contains("PageTitle"))
                throw new InvalidOperationException("Navigation zur App-Information fehlgeschlagen.");
            model.ShowHomeCommand.Execute(null);
            if (model.ShowAbout || model.PageTitle != homeTitle)
                throw new InvalidOperationException("Navigation zur Übersicht fehlgeschlagen.");
            window.Dispatcher.Invoke(() => { }, DispatcherPriority.Render);
            if (!window.IsVisible || window.ActualWidth < window.MinWidth)
                throw new InvalidOperationException("Startfenster wurde nicht korrekt geladen.");
            if (args.Length == 1)
            {
                var content = (FrameworkElement)window.Content;
                var bitmap = new RenderTargetBitmap((int)content.ActualWidth, (int)content.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(content);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var stream = System.IO.File.Create(args[0]);
                encoder.Save(stream);
            }
            window.Close();
            app.Shutdown();
            Console.WriteLine("PASS: Startfenster, XAML-Ressourcen und Navigation.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }
}
