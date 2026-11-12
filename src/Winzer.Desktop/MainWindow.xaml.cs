using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DrawnUi.Draw;
using DrawnUi.Wpf;
using Winzer.Ui;

namespace Winzer.Desktop;

/// <summary>Das Fenster hält genau eine gezeichnete Fläche; alles Sichtbare kommt aus <see cref="AppHost"/>.</summary>
public partial class MainWindow : Window
{
    readonly AppHost app;
    readonly DrawnUiElement surface;
    bool rebuildPending;

    public MainWindow(AppHost app, bool accelerated = true)
    {
        InitializeComponent();
        this.app = app;
        surface = new DrawnUiElement { RenderingMode = accelerated ? RenderingModeType.Accelerated : RenderingModeType.Default };
        Root.Children.Add(surface);
        app.Changed += ScheduleRebuild;
        app.QuitRequested += Close;
        Root.SizeChanged += (_, _) => ScheduleRebuild();
        PreviewKeyDown += OnKey;
        Loaded += (_, _) => Rebuild();
    }

    /// <summary>Mehrere Änderungen in einem Zug führen zu einem einzigen Neuaufbau.</summary>
    void ScheduleRebuild()
    {
        if (rebuildPending) return;
        rebuildPending = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            rebuildPending = false;
            Rebuild();
        });
    }

    void Rebuild()
    {
        if (Root.ActualWidth < 1 || Root.ActualHeight < 1) return;
        var previous = surface.Content as SkiaControl;
        surface.Content = app.Build(Root.ActualWidth, Root.ActualHeight);
        previous?.Dispose();
    }

    void OnKey(object sender, KeyEventArgs e)
    {
        var key = e.Key switch
        {
            Key.Space => "Space",
            Key.Enter => "Enter",
            Key.Escape => "Escape",
            Key.Tab => "Tab",
            >= Key.D1 and <= Key.D9 => ((int)e.Key - (int)Key.D0).ToString(),
            >= Key.NumPad1 and <= Key.NumPad9 => ((int)e.Key - (int)Key.NumPad0).ToString(),
            >= Key.A and <= Key.Z => e.Key.ToString(),
            _ => null,
        };
        if (key is not null && app.HandleKey(key)) e.Handled = true;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern bool SetCursorPos(int x, int y);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern bool GetCursorPos(out System.Drawing.Point point);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extra);

    /// <summary>
    /// Klickt mit der echten Maus an eine Stelle der Entwurfsfläche und setzt den Zeiger danach zurück –
    /// die verlässlichste Prüfung, weil sie denselben Weg nimmt wie der Spieler. Das Fenster muss vorn liegen.
    /// </summary>
    public async Task RealClick(double designX, double designY)
    {
        var scale = Math.Min(Root.ActualWidth / Kit.DesignWidth, Root.ActualHeight / Kit.DesignHeight);
        var local = new Point((Root.ActualWidth - Kit.DesignWidth * scale) / 2 + designX * scale, (Root.ActualHeight - Kit.DesignHeight * scale) / 2 + designY * scale);
        var screen = Root.PointToScreen(local);
        GetCursorPos(out var before);
        SetCursorPos((int)screen.X, (int)screen.Y);
        await Task.Delay(120);
        mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero);
        await Task.Delay(60);
        mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
        await Task.Delay(120);
        SetCursorPos(before.X, before.Y);
    }

    /// <summary>Speichert den Fensterinhalt als PNG – für Bildschirmfotos in Tests und Dokumentation.</summary>
    public void SaveScreenshot(string file)
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        var bitmap = new RenderTargetBitmap((int)(Root.ActualWidth * dpi.DpiScaleX), (int)(Root.ActualHeight * dpi.DpiScaleY),
            dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
        bitmap.Render(Root);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(file);
        encoder.Save(stream);
    }
}
