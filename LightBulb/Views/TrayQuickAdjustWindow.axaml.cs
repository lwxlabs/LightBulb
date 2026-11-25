using Avalonia.Controls;
using LightBulb.ViewModels;

namespace LightBulb.Views;

public partial class TrayQuickAdjustWindow : Window
{
    public TrayQuickAdjustWindow()
    {
        InitializeComponent();
    }

    public TrayQuickAdjustWindow(TrayQuickAdjustViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }
}
