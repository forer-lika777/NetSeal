using Microsoft.Extensions.DependencyInjection;
using NetSeal.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace NetSeal.Core.Services.AppSetting;

public class UiSettings([FromKeyedServices("client")] ISettingsStore settingsStore) : IUiSettings
{
    private readonly ISettingsStore settingsStore = settingsStore;

    public IKeyItem<LightDarkModes> LightDarkMods { get; } = new KeyItem<LightDarkModes>(settingsStore, nameof(LightDarkMods), LightDarkModes.FollowSystem, LightDarkModesJsonContext.Default);
}
