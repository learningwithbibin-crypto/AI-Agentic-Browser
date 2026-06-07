using AgenticBrowserAI.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using Windows.UI;

namespace StarAIAgentUI.UIServices
{

    public class TaskStatusRow : Grid
    {
        private readonly TextBlock textBlock;
        private readonly Grid iconContainer;
        private readonly StackPanel actionPanel;
        private readonly HyperlinkButton restartButton;
        private readonly HyperlinkButton cancelButton;

        public int TaskId { get; }

        public event Action<int> RestartRequested;
        public event Action<int> CancelRequested;

        public TaskStatusRow(int taskId, string taskName)
        {
            TaskId = taskId;

            ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Icon
            iconContainer = new Grid
            {
                Width = 14,
                Height = 14,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(iconContainer, 0);
            Children.Add(iconContainer);

            // Text
            textBlock = new TextBlock
            {
                Text = taskName,
                Margin = new Thickness(10, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 13
            };
            Grid.SetColumn(textBlock, 1);
            Children.Add(textBlock);

            // Actions
            actionPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 0, 0)
            };

            restartButton = new HyperlinkButton
            {
                Content = "Restart",
                Foreground = GetBrush("#10B981"), // Green
                Visibility = Visibility.Collapsed,
                FontSize = 11
            };

            cancelButton = new HyperlinkButton
            {
                Content = "Cancel",
                Foreground = GetBrush("#EF4444"), // Red
                Visibility = Visibility.Collapsed,
                FontSize = 11,
            };

            restartButton.Click += (s, e) => RestartRequested?.Invoke(TaskId);
            cancelButton.Click += (s, e) => CancelRequested?.Invoke(TaskId);

            actionPanel.Children.Add(restartButton);
            actionPanel.Children.Add(cancelButton);

            Grid.SetColumn(actionPanel, 2);
            Children.Add(actionPanel);

            UpdateState(AITaskStatus.UnInitialised);
        }

        public void UpdateState(AITaskStatus state)
        {
            iconContainer.Children.Clear();

            restartButton.Visibility = Visibility.Collapsed;
            cancelButton.Visibility = Visibility.Collapsed;

            switch (state)
            {
                case AITaskStatus.Processing:
                    iconContainer.Children.Add(new ProgressRing
                    {
                        IsActive = true,
                        Width = 14,
                        Height = 14,
                        Foreground = GetBrush("#60A5FA")
                    });

                    textBlock.Foreground = GetBrush("#60A5FA");
                    textBlock.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
                    textBlock.TextDecorations = Windows.UI.Text.TextDecorations.None;

                    cancelButton.Visibility = Visibility.Visible;
                    break;

                case AITaskStatus.Completed:
                    iconContainer.Children.Add(new FontIcon
                    {
                        FontFamily = new BaseMediumFontFamily(),
                        Glyph = "\xE73E;",
                        Foreground = GetBrush("#10B981"),
                        FontSize = 13
                    });

                    textBlock.Foreground = GetBrush("#A7F3D0");
                    textBlock.FontWeight = Microsoft.UI.Text.FontWeights.Normal;

                    restartButton.Visibility = Visibility.Visible;
                    break;

                case AITaskStatus.Cancelled:
                    iconContainer.Children.Add(new FontIcon
                    {
                        FontFamily = new BaseMediumFontFamily(),
                        Glyph = "\xEA39;",
                        Foreground = GetBrush("#D13438"),
                        FontSize = 13
                    });

                    textBlock.Foreground = GetBrush("#FCA5A5");
                    restartButton.Visibility = Visibility.Visible;
                    break;

                case AITaskStatus.Failed:
                    iconContainer.Children.Add(new FontIcon
                    {
                        FontFamily = new BaseMediumFontFamily(),
                        Glyph = "\xEA39;",
                        Foreground = GetBrush("#EF4444"),
                        FontSize = 13
                    });

                    textBlock.Foreground = GetBrush("#FCA5A5");
                    textBlock.TextDecorations = Windows.UI.Text.TextDecorations.Strikethrough;

                    restartButton.Visibility = Visibility.Visible;
                    break;

                case AITaskStatus.UnInitialised:
                    iconContainer.Children.Add(new FontIcon
                    {
                        FontFamily = new BaseMediumFontFamily(),
                        //Glyph = "\xE711;",
                        Foreground = GetBrush("#9CA3AF"),
                        FontSize = 13
                    });

                    textBlock.Foreground = GetBrush("#9CA3AF");
                    break;
                case AITaskStatus.Pending:
                default:
                    iconContainer.Children.Add(new FontIcon
                    {
                        FontFamily = new BaseMediumFontFamily(),
                        Glyph = "\xE916;",
                        Foreground = GetBrush("#9CA3AF"),
                        FontSize = 13
                    });

                    textBlock.Foreground = GetBrush("#9CA3AF");

                    cancelButton.Visibility = Visibility.Visible;
                    break;
            }
        }

        private SolidColorBrush GetBrush(string hex)
            => new SolidColorBrush(ColorHelper.ToColor(hex));

        private class BaseMediumFontFamily : FontFamily
        {
            public BaseMediumFontFamily() : base("Segoe MDL2 Assets") { }
        }
    }

    // Quick color helper function for hex parsing
    public static class ColorHelper
    {
        public static Color ToColor(string hex)
        {
            hex = hex.Replace("#", "");
            byte r = byte.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
            byte g = byte.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
            byte b = byte.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
            return Color.FromArgb(255, r, g, b);
        }
    }
}
