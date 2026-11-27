using Microsoft.Extensions.DependencyInjection;
using NetSeal.Core.Models;

namespace NetSeal.Core.Services.AppSetting;

public class UiSettings([FromKeyedServices("client")] ISettingsStore settingsStore) : IUiSettings
{
    public IKeyItem<LightDarkModes> LightDarkMods { get; } = new KeyItem<LightDarkModes>(settingsStore, nameof(LightDarkMods), LightDarkModes.FollowSystem, LightDarkModesJsonContext.Default);

    public IKeyItem<bool> EnableBackground { get; } = new KeyItem<bool>(settingsStore, nameof(EnableBackground), false);
}
