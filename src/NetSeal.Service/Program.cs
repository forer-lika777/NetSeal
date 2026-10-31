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

builder.Services.AddSingleton<IRasDialer, RasDialer>();
builder.Services.AddSingleton<IAppSettings, AppSettings>();
builder.Services.AddSingleton<ISettingsStore, LocalSettingsStore>();

builder.Services.AddSingleton<INetworkService, NetworkService>();
builder.Services.AddSingleton<INetworkAdapterService, NetworkAdapterService>();

bool mock = false;

if (mock)
{
    MockNetworkStatusManagerService service = new MockNetworkStatusManagerService();
    builder.Services.AddSingleton<ICoreManager>(service);
    builder.Services.AddSingleton<IRasDialer>(service);
}
else
{
    builder.Services.AddSingleton<ICoreManager, CoreManager>();
    builder.Services.AddSingleton<IRasDialer, RasDialer>();
}

builder.Services.AddSingleton<Worker>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<Worker>());
builder.Services.AddHostedService<PipeServer>();
builder.Services.AddSingleton<ServiceState>();

var host = builder.Build();
host.Run();
