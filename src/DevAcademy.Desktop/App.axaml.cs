using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using DevAcademy.Desktop.Composition;
using DevAcademy.Desktop.ViewModels;
using DevAcademy.Desktop.Views;
using Microsoft.Extensions.DependencyInjection;

namespace DevAcademy.Desktop;

public sealed class App : Avalonia.Application
{
    private ServiceProvider? _services;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

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
        }
        base.OnFrameworkInitializationCompleted();
    }
}
