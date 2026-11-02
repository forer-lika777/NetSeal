using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using NetSeal.Core.Models;
using NetSeal.Core.Services.AppSetting;
using NetSeal.WinUI.Services;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace NetSeal.WinUI.ViewModels;

public partial class SettingsPageModel : ObservableObject
{
    private readonly IUiSettings settings;
    private readonly IUiDispatcher dispatcher;

    public SettingsPageModel(IUiSettings settings, IUiDispatcher dispatcher)
    {
        this.settings = settings;
        this.dispatcher = dispatcher;
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        await dispatcher.InvokeAsync(async () => SelectedLightDarkModeIndex = (int)await settings.LightDarkMods.GetValueAsync());
    }

    [ObservableProperty]
    public partial int SelectedLightDarkModeIndex { get; set; } = 0;

    async partial void OnSelectedLightDarkModeIndexChanged(int oldValue, int newValue)
    {
        LightDarkModes mode = (LightDarkModes)newValue;

        if (App.Current.Window?.Content is FrameworkElement rootElement)
            rootElement.RequestedTheme = (ElementTheme)mode;

        await settings.LightDarkMods.SetValueAsync(mode);
    }

    [ObservableProperty]
    public partial ObservableCollection<string> LightDarkModeDisplayCollection { get; set; } = ["跟随系统", "浅色模式", "深色模式"];
}
