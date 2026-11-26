using NetSeal.Core.Services.AppSetting;
using NetSeal.Core.Services.Network;
using NetSeal.Core.Services.Ras;
using NetSeal.Core.Services.SystemPowerRegister;
using NetSeal.Service;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options => options.ServiceName = "NetSeal");

Log.Logger = new LoggerConfiguration()
#if DEBUG
    .MinimumLevel.Debug()
#else
    .MinimumLevel.Information()
#endif
    .WriteTo.File(
        path: @"C:\ProgramData\NetSeal\logs\.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30)
    .WriteTo.Console(
        outputTemplate: "{Timestamp:HH:mm:ss.fff} [{Level:u3}] {SourceContext}{NewLine}                   {Message:lj, 20}{NewLine}{Exception}",
        theme: AnsiConsoleTheme.Code)
    .CreateLogger();

builder.Logging.ClearProviders();
builder.Logging.AddSerilog(Log.Logger);

builder.Services.AddSingleton<INetworkSettings, NetworkSettings>();

var systemFolder = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
var systemPath = Path.Combine(systemFolder, "NetSeal", "network_settings.json");
var localStore = new LocalSettingsStore(systemPath);
builder.Services.AddKeyedSingleton<ISettingsStore>("service", localStore);
builder.Services.AddSingleton<ISettingsStore>(localStore);

builder.Services.AddSingleton<INetworkService, NetworkService>();
builder.Services.AddSingleton<INetworkAdapterService, NetworkAdapterService>();

builder.Services.AddSingleton(TimeProvider.System);

#if DEBUG
bool mock = false;

if (mock)
{
    MockNetworkStatusManagerService service = new MockNetworkStatusManagerService();
    builder.Services.AddSingleton<ICoreNetworkManager>(service);
    builder.Services.AddSingleton<IRasDialer>(service);
}
else
{
#endif
    builder.Services.AddSingleton<INetworkStateMonitor, NetworkStateMonitor>();
    builder.Services.AddSingleton<IRasDialer, RasDialer>();
#if DEBUG
}
#endif

builder.Services.AddSingleton<Worker>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<Worker>());
builder.Services.AddHostedService<PipeServer>();
builder.Services.AddSingleton<ServiceState>();
builder.Services.AddSingleton<SystemPowerMonitor>();

var host = builder.Build();

var monitor = host.Services.GetRequiredService<SystemPowerMonitor>();
var worker = host.Services.GetRequiredService<Worker>();

monitor.Register();
monitor.PowerModeChanged += worker.SystemPowerMonitor_PowerModeChanged;

try
{
    host.Run();
}
finally
{
    monitor.PowerModeChanged -= worker.SystemPowerMonitor_PowerModeChanged;
    monitor.UnRegister();
}
