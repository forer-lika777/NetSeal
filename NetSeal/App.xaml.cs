using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using H.NotifyIcon;
using H.NotifyIcon.Core;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using NetSeal.Pages;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using NetSeal.ViewModels;
using NetSeal.Services;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace NetSeal;
/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    public Window? Window { get; set; }
    private DispatcherQueueTimer? destroyTimer;
    public TaskbarIcon? TrayIcon { get; private set; }
    public bool HandleClosedEvents { get; set; } = true;
    private bool windowShow = true;
    private bool reallyExit;

    [LibraryImport("user32.dll", EntryPoint = "SetForegroundWindowA")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetForegroundWindow(IntPtr hWnd);

    /// <summary>
    /// Gets the current <see cref="App"/> instance in use
    /// </summary>
    public new static App Current => (App)Application.Current;

    /// <summary>
    /// Gets the <see cref="IServiceProvider"/> instance to resolve application services.
    /// </summary>
    public IServiceProvider Services { get; }

    /// <summary>
    /// Initializes the singleton application object.  This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        InitializeComponent();
        Services = ConfigureServices();
    }

    /// <summary>
    /// Configures the services for the application.
    /// </summary>
    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        services.AddSingleton<StatusPageModel>();
        services.AddSingleton<IAppSettings, AppSettings>();
        services.AddSingleton<IUiDispatcher>(sp => new WinUIDispatcher(DispatcherQueue.GetForCurrentThread()));

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        CreateTaskBarIcon();
        ShowWindow();
    }

    private void CreateTaskBarIcon()
    {
        var showWindowCommand = (XamlUICommand)Resources["ShowWindowCommand"];
        showWindowCommand.ExecuteRequested += ShowWindowCommand_ExecuteRequested;

        var exitApplicationCommand = (XamlUICommand)Resources["ExitApplicationCommand"];
        exitApplicationCommand.ExecuteRequested += ExitApplicationCommand_ExecuteRequested;

        TrayIcon = (TaskbarIcon)Resources["TrayIcon"];
        TrayIcon.ForceCreate();
    }

    private void ShowWindowCommand_ExecuteRequested(XamlUICommand sender, ExecuteRequestedEventArgs args)
    {
        ShowWindow();
    }

    private void ShowWindow()
    {
        destroyTimer?.Stop();
        destroyTimer = null;

        if (Window is not null)
        {
            if (windowShow)
            {
                Window.Activate();
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(Window);
                SetForegroundWindow(hwnd);
            }
            else
            {
                Window.AppWindow.Show(true);
            }
        }
        else
        {
            Window = new MainWindow(new RelayCommand(ShowWindow))
            {
                Content = new MainPage(),
            };

            Window.AppWindow.Closing += CloseWindow;

            Window.Activate();
        }

        windowShow = true;
    }

    private void CloseWindow(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (reallyExit)
            return;

        args.Cancel = true;
        Window?.AppWindow.Hide();

        windowShow = false;

        StartDestroyTimer();
    }

    private void StartDestroyTimer()
    {
        destroyTimer?.Stop();

        destroyTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        destroyTimer.Interval = TimeSpan.FromMinutes(1);
        destroyTimer.IsRepeating = false;
        destroyTimer.Tick += (s, e) => DestroyWindow();
        destroyTimer.Start();
    }

    private void DestroyWindow()
    {
        if (Window is null)
            return;

        Window.AppWindow.Closing -= CloseWindow;
        Window?.Content = null;
        Window?.Close();
        if (Window is MainWindow window)
        {
            window.ShowWindowCommand = null;
        }

        Window = null;
        destroyTimer = null;

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    private void ExitApplicationCommand_ExecuteRequested(object? _, ExecuteRequestedEventArgs args)
    {
        reallyExit = true;
        HandleClosedEvents = false;
        TrayIcon?.Dispose();
        Window?.Close();

        Environment.Exit(0);
    }
}
