using System.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorWPFApp2;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        this.InitializeComponent();

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddWpfBlazorWebView();
#if DEBUG
        serviceCollection.AddBlazorWebViewDeveloperTools();
#endif

        this.Resources.Add("services", serviceCollection.BuildServiceProvider());
    }
}