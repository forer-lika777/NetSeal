namespace NetSeal.Service;

public class ServiceState
{
    private readonly SemaphoreSlim reloadSignal = new(0);

    public void TriggerReload() => reloadSignal.Release();

    public async Task WaitForReloadAsync(TimeSpan timeout, CancellationToken ct)
    {
        await reloadSignal.WaitAsync(timeout, ct);
    }
}
