using Avalonia;
using Avalonia.Headless;
using DevAcademy.Desktop;

[assembly: AvaloniaTestApplication(typeof(DevAcademy.Desktop.Tests.Support.TestAppBuilder))]

namespace DevAcademy.Desktop.Tests.Support;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
