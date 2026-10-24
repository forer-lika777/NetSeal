using Microsoft.Extensions.Hosting.WindowsServices;
using NetSeal.Core.Services;
using NetSeal.Core.Services.Ras;
using NetSeal.Service;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "NetSeal";
});

builder.Services.AddSingleton<IRasDialer, RasDialer>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
