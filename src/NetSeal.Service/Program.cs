using NetSeal.Core.Services;
using NetSeal.Core.Services.AppSetting;
using NetSeal.Core.Services.Network;
using NetSeal.Core.Services.Ras;
using NetSeal.Core.Services.SystemPowerRegister;
using NetSeal.Service;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "NetSeal";
});

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
    .CreateLogger();

builder.Logging.ClearProviders();
builder.Logging.AddSerilog(Log.Logger);
builder.Logging.AddConsole();
#if DEBUG
builder.Logging.AddFilter<Microsoft.Extensions.Logging.Console.ConsoleLoggerProvider>(null, LogLevel.Debug);
#else
builder.Logging.AddFilter<Microsoft.Extensions.Logging.Console.ConsoleLoggerProvider>(null, LogLevel.Information);
#endif

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
    builder.Services.AddSingleton<ICoreManager>(service);
    builder.Services.AddSingleton<IRasDialer>(service);
}
else
{
#endif
    builder.Services.AddSingleton<ICoreNetworkManager, CoreNetworkManager>();
    builder.Services.AddSingleton<IRasDialer, RasDialer>();
#if DEBUG
}
#endif

builder.Services.AddSingleton<Worker>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<Worker>());
builder.Services.AddHostedService<PipeServer>();
builder.Services.AddSingleton<ServiceState>();

var host = builder.Build();

var worker = host.Services.GetRequiredService<Worker>();

SystemPowerMonitor.Register();
SystemPowerMonitor.PowerModeChanged += worker.SystemPowerMonitor_PowerModeChanged;

try
{
    host.Run();
}
finally
{
    SystemPowerMonitor.PowerModeChanged -= worker.SystemPowerMonitor_PowerModeChanged;
    SystemPowerMonitor.UnRegister();
}
