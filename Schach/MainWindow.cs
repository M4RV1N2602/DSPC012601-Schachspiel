using Avalonia.Controls;

namespace Schach;

public sealed class MainWindow : Window
{
    public MainWindow()
    {
        Content = new MainView();
    }
}
