using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Controls;
using DevAcademy.Desktop.Composition;
using DevAcademy.Desktop.Localization;
using DevAcademy.Desktop.ViewModels;
using DevAcademy.Desktop.Views;
using Microsoft.Extensions.DependencyInjection;

namespace DevAcademy.Desktop;

public sealed class App : Avalonia.Application
{
    private ServiceProvider? _services;

    private MainViewModel? _main;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        // Replaces Avalonia's default application menu ("About Avalonia") on macOS; it has to exist before the platform
        // exports the menu, so it is set here and resolves the main view model when clicked. Other platforms ignore it.
        var about = new NativeMenuItem(Loc.T("Menu_About"));
        about.Click += async (_, _) => { if (_main is { } m) await m.ShowAboutAsync(); };
        NativeMenu.SetMenu(this, new NativeMenu { about });
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Headless tests build their own container and window; only the real desktop lifetime boots the app.
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _services = Bootstrapper.Build();
            var vm = _services.GetRequiredService<MainViewModel>();
            var window = new MainWindow { DataContext = vm };
            desktop.MainWindow = window;
            desktop.Exit += (_, _) => _services.Dispose();
            window.Opened += async (_, _) => await vm.InitializeAsync();
            _main = vm;
        }
        base.OnFrameworkInitializationCompleted();
    }
}
