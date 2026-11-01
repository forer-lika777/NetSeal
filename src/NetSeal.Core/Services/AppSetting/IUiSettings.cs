using NetSeal.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace NetSeal.Core.Services.AppSetting;

public interface IUiSettings
{
    public IKeyItem<LightDarkModes> LightDarkMods { get; }
}