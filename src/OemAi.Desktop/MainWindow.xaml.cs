using System.Collections.Specialized;
using System.Windows;
using OemAi.Desktop.ViewModels;

namespace OemAi.Desktop;

public partial class MainWindow : Window
{
    // View is constructed by DI with its ViewModel — no service locator, no `new` in XAML.
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        Loaded += async (_, _) => await viewModel.StartMonitoringAsync();

        // Pure view concern: keep the newest chat message in view.
        viewModel.Messages.CollectionChanged += (_, e) =>
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
                ChatList.ScrollIntoView(e.NewItems![0]);
        };
    }
}
