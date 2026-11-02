namespace NetSeal.Core.Services.AppSetting;

public interface ISettingsStore
{
    Task<string?> GetAsync(string name, CancellationToken ct = default);
    Task SetAsync(string name, string value, CancellationToken ct = default);
}
