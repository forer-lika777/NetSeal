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

        mockSettings.Setup(s => s.AutoConnectPolicy.GetValueAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AutoConnectPolicy { ReconnectOnWake = true, AutoConnectOnStartup = true, ReconnectOnDisconnect = true });
        mockSettings.Setup(s => s.RasCredentials.GetValueAsync(It.IsAny<CancellationToken>()))
            .Callback(() => isConnect = true)
            .ReturnsAsync(new RasCredentials { Username = "123", Password = "666" });

        mockCoreManager.Setup(c => c.GetNetworkStatusAsync())
            .ReturnsAsync(new NetworkStatus
            {
                IsPppoeNetworkConnected = isPppoeNetworkConnected || isConnect,
                IsEthernetConnected = isEthernetConnected,
                IsNotPppoeNetworkConnection = isNetworkConnectionManagedByOutside,
                IsInternetAccess = isPppoeNetworkConnected || isNetworkConnectionManagedByOutside,
                IsNetworkConnectionManagedByOutside = isNetworkConnectionManagedByOutside,
                IsPppoeConnectionManagedByOutside = isNetworkConnectionManagedByOutside,
                NetworkConnectivityLevel = NetworkConnectivityLevel.InternetAccess,
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
        justStartedField?.SetValue(worker, serviceJustStarted);

        if (simulateSleep)
        {
            var lastCheckTimeField = typeof(Worker).GetField("lastCheckTime", BindingFlags.NonPublic | BindingFlags.Instance);
            lastCheckTimeField?.SetValue(worker, fakeTime.GetLocalNow().DateTime.AddSeconds(-61));
        }

        if (!serviceJustStarted)
        {
            //serviceState.TriggerReload();
        }

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

    [Fact]
    public async Task Worker_Should_Connect_At_Startup()
    {
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

        mockSettings.Setup(s => s.AutoConnectPolicy.GetValueAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AutoConnectPolicy { ReconnectOnWake = true, AutoConnectOnStartup = true, ReconnectOnDisconnect = true });
        mockSettings.Setup(s => s.RasCredentials.GetValueAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RasCredentials { Username = "123", Password = "666" });

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

        await Task.Delay(200);

        mockRasDialer.Verify(r => r.RasConnectAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()),
            Times.Once);

        await worker.StopAsync(cts.Token);
    }

    [Fact]
    public async Task Worker_Should_Not_Connect_At_Startup_If_Internet_Access()
    {
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

        mockSettings.Setup(s => s.AutoConnectPolicy.GetValueAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AutoConnectPolicy { ReconnectOnWake = true, AutoConnectOnStartup = true, ReconnectOnDisconnect = true });
        mockSettings.Setup(s => s.RasCredentials.GetValueAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RasCredentials { Username = "123", Password = "666" });

        mockCoreManager.Setup(c => c.GetNetworkStatusAsync())
            .ReturnsAsync(new NetworkStatus
            {
                IsPppoeNetworkConnected = false,
                IsEthernetConnected = true,
                IsNotPppoeNetworkConnection = true,
                IsInternetAccess = true,
                IsNetworkConnectionManagedByOutside = true,
                IsPppoeConnectionManagedByOutside = false,
                NetworkConnectivityLevel = NetworkConnectivityLevel.InternetAccess,
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

        await Task.Delay(200);

        mockRasDialer.Verify(r => r.RasConnectAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()),
            Times.Never);

        await worker.StopAsync(cts.Token);
    }

    [Fact]
    public async Task Worker_Should_Reconnect_After_Sleep()
    {
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

        mockSettings.Setup(s => s.AutoConnectPolicy.GetValueAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AutoConnectPolicy { ReconnectOnWake = true, AutoConnectOnStartup = true, ReconnectOnDisconnect = true });
        mockSettings.Setup(s => s.RasCredentials.GetValueAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RasCredentials { Username = "123", Password = "666" });

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

        var justStartedField = typeof(Worker).GetField("serviceJustStarted", BindingFlags.NonPublic | BindingFlags.Instance);
        justStartedField?.SetValue(worker, false);

        var lastCheckTimeField = typeof(Worker).GetField("lastCheckTime", BindingFlags.NonPublic | BindingFlags.Instance);
        lastCheckTimeField?.SetValue(worker, fakeTime.GetLocalNow().DateTime.AddSeconds(-61));

        await Task.Yield();
        fakeTime.Advance(TimeSpan.FromSeconds(3.1));

        serviceState.TriggerReload();

        await Task.Delay(200);

        mockRasDialer.Verify(r => r.RasConnectAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()),
            Times.Once);

        await worker.StopAsync(cts.Token);
    }

    [Fact]
    public async Task Worker_Should_Not_Reconnect_After_Sleep_If_Network_Connected()
    {
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

        mockSettings.Setup(s => s.AutoConnectPolicy.GetValueAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AutoConnectPolicy { ReconnectOnWake = true, AutoConnectOnStartup = true, ReconnectOnDisconnect = true });
        mockSettings.Setup(s => s.RasCredentials.GetValueAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RasCredentials { Username = "123", Password = "666" });

        mockCoreManager.Setup(c => c.GetNetworkStatusAsync())
            .ReturnsAsync(new NetworkStatus
            {
                IsPppoeNetworkConnected = true,
                IsEthernetConnected = true,
                IsNotPppoeNetworkConnection = false,
                IsInternetAccess = true,
                IsNetworkConnectionManagedByOutside = false,
                IsPppoeConnectionManagedByOutside = false,
                NetworkConnectivityLevel = NetworkConnectivityLevel.InternetAccess,
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

        var lastCheckTimeField = typeof(Worker).GetField("lastCheckTime", BindingFlags.NonPublic | BindingFlags.Instance);
        lastCheckTimeField?.SetValue(worker, fakeTime.GetLocalNow().DateTime.AddSeconds(-61));

        serviceState.TriggerReload();

        await Task.Delay(200);

        mockRasDialer.Verify(r => r.RasConnectAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()),
            Times.Never);

        await worker.StopAsync(cts.Token);
    }
}
