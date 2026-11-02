using Microsoft.Extensions.Hosting.WindowsServices;
using NetSeal.Core.Services;
using NetSeal.Core.Services.AppSetting;
using NetSeal.Core.Services.Network;
using NetSeal.Core.Services.Ras;
using NetSeal.Service;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "NetSeal";
});

builder.Logging.AddEventLog(settings =>
{
    settings.SourceName = "NetSeal";
    settings.LogName = "Application";
});

builder.Services.AddSingleton<INetworkSettings, NetworkSettings>();

var systemFolder = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
var systemPath = Path.Combine(systemFolder, "NetSeal", "network_settings.json");
var localStore = new LocalSettingsStore(systemPath);
builder.Services.AddKeyedSingleton<ISettingsStore>("service", localStore);
builder.Services.AddSingleton<ISettingsStore>(localStore);

builder.Services.AddSingleton<INetworkService, NetworkService>();
builder.Services.AddSingleton<INetworkAdapterService, NetworkAdapterService>();

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
    builder.Services.AddSingleton<ICoreManager, CoreManager>();
    builder.Services.AddSingleton<IRasDialer, RasDialer>();
#if DEBUG
}
#endif

builder.Services.AddSingleton<Worker>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<Worker>());
builder.Services.AddHostedService<PipeServer>();
builder.Services.AddSingleton<ServiceState>();

var host = builder.Build();
host.Run();
