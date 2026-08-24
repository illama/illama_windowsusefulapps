using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SystemManagerPro.Views;

/// <summary>Petits éléments d'interface réutilisés par plusieurs vues (chips de statistiques, etc.).</summary>
internal static class UiHelpers
{
    public static Border Chip(string label, string value, Brush dot, FrameworkElement context)
    {
        var border = new Border
        {
            Background = (Brush)context.FindResource("BgElevated2"),
            BorderBrush = (Brush)context.FindResource("BorderBrush2"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(20),
            Padding = new Thickness(14, 7, 14, 7),
            Margin = new Thickness(0, 0, 10, 0),
        };
        var stack = new StackPanel { Orientation = Orientation.Horizontal };
        stack.Children.Add(new System.Windows.Shapes.Ellipse { Width = 8, Height = 8, Fill = dot, VerticalAlignment = VerticalAlignment.Center });
        stack.Children.Add(new TextBlock
        {
            Text = value,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(8, 0, 6, 0),
            Foreground = (Brush)context.FindResource("TextPrimary"),
            FontSize = 13,
        });
        stack.Children.Add(new TextBlock
        {
            Text = label,
            Foreground = (Brush)context.FindResource("TextSecondary"),
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
        });
        border.Child = stack;
        return border;
    }
}
