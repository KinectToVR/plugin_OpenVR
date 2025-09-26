using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amethyst.Contract;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;

namespace plugin_OpenVR;

public sealed class ConfirmationFlyout : Flyout
{
    public ConfirmationFlyout(string content, string confirmButtonText, string cancelButtonText)
    {
        ConfirmButton = new Button
        {
            Content = confirmButtonText,
            IsVisible = string.IsNullOrEmpty(confirmButtonText),
            FontSize = 15,
            FontWeight = FontWeight.SemiBold,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Height = 33,
            Margin = new Thickness(0, 10, 5, 0),
            CornerRadius = new CornerRadius(4),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Classes = { "accent" }
        };

        CancelButton = new Button
        {
            Content = cancelButtonText,
            IsVisible = string.IsNullOrEmpty(cancelButtonText),
            FontSize = 15,
            FontWeight = FontWeight.SemiBold,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Height = 33,
            Margin = new Thickness(5, 10, 0, 0),
            CornerRadius = new CornerRadius(4),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        Content = new StackPanel()
        {
            Orientation = Orientation.Vertical,
            Children =
            {
                new TextBlock
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    FontSize = 15,
                    FontWeight = FontWeight.SemiBold,
                    Text = content
                },
                new Grid
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    ColumnDefinitions =
                    {
                        new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                        new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
                    },
                    Children = { ConfirmButton, CancelButton }
                }
            }
        };

        ConfirmButton.Click += (_, _) =>
        {
            ConfirmationFlyoutResult = true;
            Hide(); // Hide the flyout
        };

        CancelButton.Click += (_, _) =>
        {
            ConfirmationFlyoutResult = false;
            Hide(); // Hide the flyout
        };

        Grid.SetColumn(ConfirmButton, 0);
        Grid.SetColumn(CancelButton, 1);
    }

    private Button ConfirmButton { get; }
    private Button CancelButton { get; }
    private bool ConfirmationFlyoutResult { get; set; }

    private static SemaphoreSlim FlyoutExitSemaphore { get; } = new(0);

    public static async Task<bool> HandleButtonConfirmationFlyout(
        Control showAtElement, IAmethystHost host,
        string content, string confirmButtonText, string cancelButtonText)
    {
        var flyout = new ConfirmationFlyout(content, confirmButtonText, cancelButtonText);

        flyout.Closed += (_, _) => FlyoutExitSemaphore.Release();
        flyout.Opening += (_, _) => host?.PlayAppSound(SoundType.Show);
        flyout.Closing += (_, _) => host?.PlayAppSound(SoundType.Hide);

        // Show the confirmation flyout
        flyout.ShowAt(showAtElement);

        // Wait for the flyout to close
        await FlyoutExitSemaphore.WaitAsync();

        // Wait a bit
        await Task.Delay(1200);

        // Return the result
        return flyout.ConfirmationFlyoutResult;
    }
}
