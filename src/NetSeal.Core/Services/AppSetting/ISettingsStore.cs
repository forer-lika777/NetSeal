using System;
using System.Collections.Generic;
using System.Text;

namespace NetSeal.Core.Services.AppSetting;

public interface ISettingsStore
{
    Task<string?> GetAsync(string name, CancellationToken ct = default);
    Task SetAsync(string name, string value, CancellationToken ct = default);
}
