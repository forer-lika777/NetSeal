using NetSeal.Core.Services.Ras;

namespace NetSeal.Core.Tests;

[TestClass]
public sealed class Test1
{
    [TestMethod]
    public async Task TestMethod1()
    {
        RasDialer rasDialer = new RasDialer();
    }

    public TestContext TestContext { get; set; }
}
