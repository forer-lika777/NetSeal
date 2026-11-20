using Microsoft.Extensions.Logging;
using NetSeal.Core.Services.Network;
using NetSeal.Core.Services.Ras;

namespace NetSeal.Core.Tests;

public class UnitTest1
{
    private readonly ITestOutputHelper outputHelper;

    public UnitTest1(ITestOutputHelper outputHelper)
    {
        this.outputHelper = outputHelper;
    }

    [Fact]
    public async Task Test1()
    {
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddXUnit(outputHelper);
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        var adapterservice = new NetworkAdapterService();
        var networkservice = new NetworkService(adapterservice, new RasDialer());

        var networks = await networkservice.GetActiveNetworksAsync();
    }
}
