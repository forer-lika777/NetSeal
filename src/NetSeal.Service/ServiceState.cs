namespace NetSeal.Service;

public class ServiceState
{
    private readonly SemaphoreSlim reloadSignal = new(0);

    private bool triggered = false;

    public void TriggerReload()
    {
        triggered = true;
        reloadSignal.Release();
    }

    public async Task<bool> WaitForReloadAsync(TimeSpan timeout, CancellationToken ct)
    {
        triggered = false;
        await reloadSignal.WaitAsync(timeout, ct);
        return triggered;
    }
}
