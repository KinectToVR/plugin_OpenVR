// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amethyst.Plugins.Contract;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace plugin_OpenVR;

public sealed class ConfirmationFlyout : Flyout
{
    public ConfirmationFlyout(string content, string confirmButtonText, string cancelButtonText)
    {
        ConfirmButton = new Button
        {
            Content = confirmButtonText,
            Visibility = (Visibility)Convert.ToInt32(string.IsNullOrEmpty(confirmButtonText)),
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Height = 33,
            Margin = new Thickness(0, 10, 5, 0),
            CornerRadius = new CornerRadius(4),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Style = Application.Current.Resources["AccentButtonStyle"] as Style
        };

        CancelButton = new Button
        {
            Content = cancelButtonText,
            Visibility = (Visibility)Convert.ToInt32(string.IsNullOrEmpty(cancelButtonText)),
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Height = 33,
            Margin = new Thickness(5, 10, 0, 0),
            CornerRadius = new CornerRadius(4),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        Content = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }
            },
            Children =
            {
                new Grid
                {
                    ColumnDefinitions =
                    {
                        new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                        new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
                    },
                    Children = { ConfirmButton, CancelButton }
                },
                new TextBlock
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch,
                    FontSize = 15, FontWeight = FontWeights.SemiBold, Text = content
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

        Grid.SetRow((Content as Grid)!.Children.First() as Grid, 1);

        Grid.SetColumn(ConfirmButton, 0);
        Grid.SetColumn(CancelButton, 1);
    }

    private Button ConfirmButton { get; }
    private Button CancelButton { get; }
    private bool ConfirmationFlyoutResult { get; set; }

    private static SemaphoreSlim FlyoutExitSemaphore { get; } = new(0);

    public static async Task<bool> HandleButtonConfirmationFlyout(
        UIElement showAtElement, IAmethystHost host,
        string content, string confirmButtonText, string cancelButtonText)
    {
        var flyout = new ConfirmationFlyout(content, confirmButtonText, cancelButtonText);

        flyout.Closed += (_, _) => FlyoutExitSemaphore.Release();
        flyout.Opening += (_, _) => host?.PlayAppSound(SoundType.Show);
        flyout.Closing += (_, _) => host?.PlayAppSound(SoundType.Hide);

        // Show the confirmation flyout
        flyout.ShowAt(showAtElement, new FlyoutShowOptions { Placement = FlyoutPlacementMode.Bottom });

        // Wait for the flyout to close
        await FlyoutExitSemaphore.WaitAsync();

        // Wait a bit
        await Task.Delay(1200);

        // Return the result
        return flyout.ConfirmationFlyoutResult;
    }
}