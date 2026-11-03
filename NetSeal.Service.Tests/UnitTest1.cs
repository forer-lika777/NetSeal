using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using NetSeal.Core.Models;
using NetSeal.Core.Services;
using NetSeal.Core.Services.AppSetting;
using NetSeal.Core.Services.Ras;
using System.Reflection;

namespace NetSeal.Service.Tests;

public class WorkerTests
{
    private readonly ITestOutputHelper outputHelper;

    public WorkerTests(ITestOutputHelper outputHelper)
    {
        this.outputHelper = outputHelper;
    }

    [Theory]
    // 场景1：开机自启，网络正常，刚启动 -> 应该连接 (true)
    [InlineData(false, true, false, true, false, false, true, false, true)]

    // 场景2：断线重连，网络正常，意外断开 -> 应该连接 (true)
    [InlineData(false, false, true, true, false, false, false, false, true)]

    // 场景3：睡眠唤醒，网络正常，模拟睡眠 -> 应该连接 (true)
    [InlineData(true, false, false, true, false, false, false, true, true)]

    // 场景4：网线没插 -> 绝对不连 (false)
    [InlineData(true, true, true, false, false, false, false, false, false)]

    // 场景5：已经被外部接管了 -> 绝对不连 (false)
    [InlineData(true, true, true, true, false, true, false, false, false)]

    // 场景6：PPPoE 已经连上了 -> 绝对不连 (false)
    [InlineData(true, true, true, true, true, false, false, false, false)]
    public async Task Worker_Combo_Tests(
        bool reconnectOnWake,
        bool autoConnectOnStartup,
        bool reconnectOnDisconnect,
        bool isEthernetConnected,
        bool isPppoeNetworkConnected,
        bool isNetworkConnectionManagedByOutside,
        bool serviceJustStarted,
        bool simulateSleep,
        bool result)
    {
        bool isConnect = false;

        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddXUnit(outputHelper);
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        var logger = loggerFactory.CreateLogger<Worker>();

        var fakeTime = new FakeTimeProvider();

        var mockRasDialer = new Mock<IRasDialer>();
        var mockSettings = new Mock<INetworkSettings>();
        var mockCoreManager = new Mock<ICoreManager>();
        var serviceState = new ServiceState();

        var network = new NetworkItem { Id = new Guid("EF528CED-F357-4388-A701-C69C0A20EBE3") };

        var status = new NetworkStatus
        {
            IsPppoeNetworkConnected = isPppoeNetworkConnected || isConnect,
            IsEthernetConnected = isEthernetConnected,
            IsNotPppoeNetworkConnection = isNetworkConnectionManagedByOutside,
            IsInternetAccess = isPppoeNetworkConnected || isNetworkConnectionManagedByOutside,
            IsNetworkConnectionManagedByOutside = isNetworkConnectionManagedByOutside,
            IsPppoeConnectionManagedByOutside = isNetworkConnectionManagedByOutside,
            NetworkConnectivityLevel = NetworkConnectivityLevel.InternetAccess,
            ActiveEthernetNetworks = [network],
        };

        var worker = new Worker(
            logger,
            mockRasDialer.Object,
            mockSettings.Object,
            mockCoreManager.Object,
            serviceState,
            fakeTime
        );

        mockRasDialer.Setup(r => r.RasConnectAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(async () =>
            {
                status.IsPppoeNetworkConnected = true;
                await worker.UpdateNetworkStatusAsync();
            });

        mockSettings.Setup(s => s.AutoConnectPolicy.GetValueAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AutoConnectPolicy { ReconnectOnWake = true, AutoConnectOnStartup = true, ReconnectOnDisconnect = true });
        mockSettings.Setup(s => s.RasCredentials.GetValueAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RasCredentials { Username = "123", Password = "666" });
        mockSettings.Setup(s => s.SelectedNetwork.GetValueAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(network);

        mockCoreManager.Setup(c => c.GetNetworkStatusAsync())
            .ReturnsAsync(status);

        var cts = new CancellationTokenSource();
        Task task = worker.StartAsync(cts.Token);

        await Task.Delay(10);

        await Task.Yield();
        fakeTime.Advance(TimeSpan.FromSeconds(3.1));

        var justStartedField = typeof(Worker).GetField("serviceJustStarted", BindingFlags.NonPublic | BindingFlags.Instance);
        justStartedField?.SetValue(worker, serviceJustStarted);

        if (simulateSleep)
        {
            var lastCheckTimeField = typeof(Worker).GetField("lastCheckTime", BindingFlags.NonPublic | BindingFlags.Instance);
            lastCheckTimeField?.SetValue(worker, fakeTime.GetLocalNow().DateTime.AddSeconds(-61));
        }

        if (!serviceJustStarted)
        {
            serviceState.TriggerReload();
        }

        await Task.Delay(1000);

        mockRasDialer.Verify(r => r.RasConnectAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()),
            result ? Times.Once : Times.Never);

        await worker.StopAsync(cts.Token);
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    public async Task Modify_Settings_Tests(bool hasCredentials, bool hasMatchedNetwork, bool result)
    {
        bool isConnect = false;

        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddXUnit(outputHelper);
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        var logger = loggerFactory.CreateLogger<Worker>();

        var fakeTime = new FakeTimeProvider();

        var mockRasDialer = new Mock<IRasDialer>();
        var mockSettings = new Mock<INetworkSettings>();
        var mockCoreManager = new Mock<ICoreManager>();
        var serviceState = new ServiceState();

        var network = new NetworkItem { Id = new Guid("EF528CED-F357-4388-A701-C69C0A20EBE3") };

        mockSettings.Setup(s => s.AutoConnectPolicy.GetValueAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AutoConnectPolicy { ReconnectOnWake = true, AutoConnectOnStartup = true, ReconnectOnDisconnect = true });
        mockSettings.Setup(s => s.RasCredentials.GetValueAsync(It.IsAny<CancellationToken>()))
            .Callback(() => isConnect = true)
            .ReturnsAsync(new RasCredentials { Username = hasCredentials ? "123" : "", Password = hasCredentials ? "shazi" : "" });
        mockSettings.Setup(s => s.SelectedNetwork.GetValueAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(network);

        mockCoreManager.Setup(c => c.GetNetworkStatusAsync())
            .ReturnsAsync(new NetworkStatus
            {
                IsPppoeNetworkConnected = false,
                IsEthernetConnected = true,
                IsNotPppoeNetworkConnection = false,
                IsInternetAccess = false,
                IsNetworkConnectionManagedByOutside = false,
                IsPppoeConnectionManagedByOutside = false,
                NetworkConnectivityLevel = NetworkConnectivityLevel.ConstrainedInternetAccess,
                ActiveEthernetNetworks = hasMatchedNetwork ? [network] : [],
            });

        var worker = new Worker(
            logger,
            mockRasDialer.Object,
            mockSettings.Object,
            mockCoreManager.Object,
            serviceState,
            fakeTime
        );

        var cts = new CancellationTokenSource();
        Task task = worker.StartAsync(cts.Token);

        await Task.Delay(10);

        await Task.Yield();
        fakeTime.Advance(TimeSpan.FromSeconds(3.1));

        var justStartedField = typeof(Worker).GetField("serviceJustStarted", BindingFlags.NonPublic | BindingFlags.Instance);
        justStartedField?.SetValue(worker, false);

        await Task.Delay(200);

        mockRasDialer.Verify(r => r.RasConnectAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()),
            result ? Times.Once : Times.Never);

        await worker.StopAsync(cts.Token);
    }
}
