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
builder.Services.AddSingleton<ICoreManager, CoreManager>();
builder.Services.AddSingleton<Worker>();
builder.Services.AddHostedService<Worker>();
builder.Services.AddHostedService<PipeServer>();
builder.Services.AddSingleton<ServiceState>();

var host = builder.Build();
host.Run();
