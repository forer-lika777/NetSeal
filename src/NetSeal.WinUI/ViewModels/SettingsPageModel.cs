using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Windowing;
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
        await dispatcher.InvokeAsync(async () =>
        {
            SelectedLightDarkModeIndex = (int)await settings.LightDarkMods.GetValueAsync();
            EnableBackground = await settings.EnableBackground.GetValueAsync();
        });
    }

    [ObservableProperty]
    public partial int SelectedLightDarkModeIndex { get; set; } = 0;

    async partial void OnSelectedLightDarkModeIndexChanged(int oldValue, int newValue)
    {
        LightDarkModes mode = (LightDarkModes)newValue;

        var window = App.Current.Window;

        if (window is not null)
        {
            // ElementTheme 的枚举值：https://learn.microsoft.com/uwp/api/windows.ui.xaml.elementtheme?view=winrt-26100
            if (window.Content is FrameworkElement rootElement)
                rootElement.RequestedTheme = (ElementTheme)mode;

            // TitleBarTheme 的枚举值：https://learn.microsoft.com/windows/windows-app-sdk/api/winrt/microsoft.ui.windowing.titlebartheme?view=windows-app-sdk-2.0
            window.AppWindow.TitleBar.PreferredTheme = (TitleBarTheme)((int)mode + 1);
        }

        await settings.LightDarkMods.SetValueAsync(mode);
    }

    [ObservableProperty]
    public partial ObservableCollection<string> LightDarkModeDisplayCollection { get; set; } = ["跟随系统", "浅色模式", "深色模式"];

    [ObservableProperty]
    public partial bool EnableBackground { get; set; } = true;

    async partial void OnEnableBackgroundChanged(bool oldValue, bool newValue)
    {
        StrongReferenceMessenger.Default.Send<ModifyEnableBackground>(new ModifyEnableBackground(newValue));
        await settings.EnableBackground.SetValueAsync(newValue);
    }
}
