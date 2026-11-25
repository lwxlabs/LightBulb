using Avalonia;
using Avalonia.Controls;

namespace LightBulb.Views.Components.Settings;

public partial class ToneProfileSettingsTabView : UserControl
{
    private const double StackedLayoutThreshold = 800;

    public ToneProfileSettingsTabView()
    {
        InitializeComponent();
        SizeChanged += (_, _) => UpdateResponsiveLayout();
        AttachedToVisualTree += (_, _) => UpdateResponsiveLayout();
    }

    private void UpdateResponsiveLayout()
    {
        if (ResponsiveRoot is null)
            return;

        var availableWidth = Bounds.Width;
        var stacked = availableWidth > 0 && availableWidth < StackedLayoutThreshold;

        ResponsiveRoot.ColumnDefinitions.Clear();
        ResponsiveRoot.RowDefinitions.Clear();

        if (stacked)
        {
            ResponsiveRoot.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            ResponsiveRoot.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            ResponsiveRoot.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            ResponsiveRoot.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            Grid.SetRow(ProfileListPanel, 0);
            Grid.SetColumn(ProfileListPanel, 0);
            Grid.SetRow(ProfileEditorPanel, 1);
            Grid.SetColumn(ProfileEditorPanel, 0);
            Grid.SetRow(EmptyProfilePanel, 1);
            Grid.SetColumn(EmptyProfilePanel, 0);
            Grid.SetRow(ProfileHint, 2);
            Grid.SetColumn(ProfileHint, 0);
            Grid.SetColumnSpan(ProfileHint, 1);
            ProfileList.MaxHeight = 230;
        }
        else
        {
            ResponsiveRoot.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
            ResponsiveRoot.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5, GridUnitType.Star) });
            ResponsiveRoot.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            ResponsiveRoot.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            Grid.SetRow(ProfileListPanel, 0);
            Grid.SetColumn(ProfileListPanel, 0);
            Grid.SetRow(ProfileEditorPanel, 0);
            Grid.SetColumn(ProfileEditorPanel, 1);
            Grid.SetRow(EmptyProfilePanel, 0);
            Grid.SetColumn(EmptyProfilePanel, 1);
            Grid.SetRow(ProfileHint, 1);
            Grid.SetColumn(ProfileHint, 0);
            Grid.SetColumnSpan(ProfileHint, 2);
            ProfileList.MaxHeight = 430;
        }
    }
}
