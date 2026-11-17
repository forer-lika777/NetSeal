using NetSeal.Core.Models;

namespace NetSeal.Core.Services.AppSetting;

public interface IUiSettings
{
    public IKeyItem<LightDarkModes> LightDarkMods { get; }
    public IKeyItem<bool> EnableBackground { get; }
}