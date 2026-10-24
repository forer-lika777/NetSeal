using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Windowing;
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
using Windows.Security.EnterpriseData;

namespace NetSeal;

public sealed partial class MainWindow : Window
{
    public IRelayCommand? ShowWindowCommand { get; set; }

    public MainWindow(RelayCommand command)
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        ShowWindowCommand = command;
        AppWindow.SetTaskbarIcon("Assets/icon.ico");
    }
}
