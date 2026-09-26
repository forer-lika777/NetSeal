using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace NetSeal.Pages;
/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class MainPage : Page
{
    private string currentTag = string.Empty;

    public MainPage()
    {
        InitializeComponent();
        NavigationView.MenuItems.Add(new NavigationViewItem { Content = "开始", Icon = new SymbolIcon(Symbol.Home), Tag = "home" });
        NavigationView.MenuItems.Add(new NavigationViewItem { Content = "状态", Icon = new FontIcon { Glyph = "\uE968" }, Tag = "status" });
        NavigationView.Tag = currentTag;
        NavigationView.SelectedItem = NavigationView.MenuItems[0];
        Navigate("home");
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
    }

    private void NavigationView_BackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args)
    {
        if (MainFrame.CanGoBack)
        {
            var page = MainFrame.BackStack.Last().SourcePageType;

            if (page.Equals(typeof(HomePage)))
            {
                currentTag = "home";
                NavigationView.SelectedItem = NavigationView.MenuItems[0];
            }
            else if (page.Equals(typeof(StatusPage)))
            {
                currentTag = "status";
                NavigationView.SelectedItem = NavigationView.MenuItems[1];
            }
            else if (page.Equals(typeof(SettingsPage)))
            {
                currentTag = "settings";
                NavigationView.SelectedItem = NavigationView.SettingsItem;
            }

            MainFrame.GoBack();
        }
    }

    private void NavigationView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.IsSettingsInvoked)
        {
            Navigate("settings");
        }
        else
        {
            Navigate(args.InvokedItemContainer.Tag.ToString() ?? "home");
        }
    }

    private void Navigate(string tag)
    {
        if (currentTag == tag)
            return;

        currentTag = tag;

        if (tag == "home")
        {
            MainFrame.Navigate(typeof(HomePage));
        }
        else if (tag == "status")
        {
            MainFrame.Navigate(typeof(StatusPage));
        }
    }
}
