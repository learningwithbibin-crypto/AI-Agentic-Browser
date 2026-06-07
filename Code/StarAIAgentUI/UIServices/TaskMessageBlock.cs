using AgenticBrowserAI.Models;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;

namespace StarAIAgentUI.UIServices
{
    public class TaskMessageBlock
    {
        private readonly StackPanel listContainer;
        private readonly Dictionary<int, TaskStatusRow> taskRows = new();

        public event Action<int> RestartTaskRequested;
        public event Action<int> CancelTaskRequested;

        public Grid RenderedElement { get; }

        public TaskMessageBlock(string introductionText)
        {
            // Outermost grid layout for chat bubble alignment
            RenderedElement = new Grid { HorizontalAlignment = HorizontalAlignment.Left, MaxWidth = 550 };

            var bubbleBorder = new Border
            {
                //Background = new SolidColorBrush(ColorHelper.ToColor("000000")),
                //BorderBrush = new SolidColorBrush(ColorHelper.ToColor("228B5CF6")),
                BorderBrush = new SolidColorBrush(ColorHelper.ToColor("FFFFFF")),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12, 12, 12, 0),
                Padding = new Thickness(16, 12, 16, 12)
            };

            var mainLayout = new StackPanel { Spacing = 10 };

            // Header Text
            var headerStack = new StackPanel { Spacing = 4 };
            headerStack.Children.Add(new TextBlock { Text = "✨ AI Agent", FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.Bold, Foreground = new SolidColorBrush(ColorHelper.ToColor("FFFFFF")) });
            headerStack.Children.Add(new TextBlock { Text = introductionText, TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(ColorHelper.ToColor("EDE7FF")), FontSize = 14 });

            mainLayout.Children.Add(headerStack);

            // List container to drop task rows into
            listContainer = new StackPanel { Spacing = 8, Margin = new Thickness(4, 0, 0, 0) };
            mainLayout.Children.Add(listContainer);

            bubbleBorder.Child = mainLayout;
            RenderedElement.Children.Add(bubbleBorder);
        }

        public void AddTask(int id, string name)
        {
            if (!taskRows.ContainsKey(id))
            {
                var row = new TaskStatusRow(id, name);
                row.RestartRequested += taskId => RestartTaskRequested?.Invoke(taskId);
                row.CancelRequested += taskId => CancelTaskRequested?.Invoke(taskId);

                taskRows.Add(id, row);
                listContainer.Children.Add(row);
            }
        }

        public void UpdateTaskState(int id, AITaskStatus newState)
        {
            if (taskRows.TryGetValue(id, out var row))
            {
                row.UpdateState(newState);
            }
        }
    }
}
