using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;

namespace Schach;

public sealed class App : Application
{
    public override void Initialize() => Styles.Add(new Avalonia.Themes.Fluent.FluentTheme());

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            RequestedThemeVariant = ThemeVariant.Light;
            desktop.MainWindow = new MainWindow();
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            RequestedThemeVariant = ThemeVariant.Light;
            singleViewPlatform.MainView = new MainView();
        }
        base.OnFrameworkInitializationCompleted();
    }
}
