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

namespace NetSeal.WinUI;

public sealed partial class MainWindow : Window
{
    public IRelayCommand? ShowWindowCommand { get; set; }

    public MainWindow(RelayCommand command)
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        ShowWindowCommand = command;
        AppWindow.SetIcon("Assets/icon.ico");

        // 获取当前窗口对应的AppWindow实例
        AppWindow appWindow = this.AppWindow;
        // 校验窗口呈现器类型，桌面应用默认为OverlappedPresenter
        if (appWindow.Presenter is OverlappedPresenter overlappingPresenter)
        {
            // 配置窗口最小宽高，单位为物理像素，可根据自身设计需求调整数值
            overlappingPresenter.PreferredMinimumWidth = 800;
            overlappingPresenter.PreferredMinimumHeight = 600;
        }
    }
}
