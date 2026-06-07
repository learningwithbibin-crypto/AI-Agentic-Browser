using AgenticBrowserAI.Agents;
using AgenticBrowserAI.Models;
using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using StarAIAgentUI.Helpers;
using StarAIAgentUI.UIServices;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Windows.Web.Http;
using WinRT;
using WinRT.Interop;


// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace StarAIAgentUI
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>

    public sealed partial class MainWindow : Window
    {
        private SystemBackdropConfiguration configurationSource;
        private DesktopAcrylicController acrylicController;
        private TaskMessageBlock currentActiveTaskBlock;
        private CancellationTokenSource? cts;


        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr LoadImage(IntPtr hinst, string lpszName, uint uType, int cxDesired, int cyDesired, uint fuLoad);

        private const uint WM_SETICON = 0x0080;
        private const uint IMAGE_ICON = 1;
        private const uint LR_LOADFROMFILE = 0x00000010;

        // --- Win32 Native Imports ---
        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        public enum DWMWINDOWATTRIBUTE
        {
            DWMWA_WINDOW_CORNER_PREFERENCE = 33
        }

        // The DWM_WINDOW_CORNER_PREFERENCE enum for DwmSetWindowAttribute's third parameter, which tells the function
        // what value of the enum to set.
        // Copied from dwmapi.h
        public enum DWM_WINDOW_CORNER_PREFERENCE
        {
            DWMWCP_DEFAULT = 0,
            DWMWCP_DONOTROUND = 1,
            DWMWCP_ROUND = 2,
            DWMWCP_ROUNDSMALL = 3
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MARGINS
        {
            public int cxLeftWidth;
            public int cxRightWidth;
            public int cyTopHeight;
            public int cyBottomHeight;
        }

        [DllImport("dwmapi.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        internal static extern void DwmSetWindowAttribute(IntPtr hwnd,
                                                     DWMWINDOWATTRIBUTE attribute,
                                                     ref DWM_WINDOW_CORNER_PREFERENCE pvAttribute,
                                                     uint cbAttribute);

        // Import dwmapi.dll and define DwmSetWindowAttribute in C# corresponding to the native function.
        [DllImport("dwmapi.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        private static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS pMarInset);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int attrSize);

        // DWM Rendering attributes constants
        private const int GWL_STYLE = -16;
        private const int WS_MAXIMIZEBOX = 0x00010000;

        [DllImport("gdi32.dll")]
        static extern IntPtr CreateRoundRectRgn(
            int left, int top, int right, int bottom,
            int widthEllipse, int heightEllipse);

        [DllImport("user32.dll")]
        static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool bRedraw);

        private DispatcherTimer colorDetectionTimer;
        private DispatcherTimer thinkingTimer;
        private TextBlock thinkingText;
        private Border thinkingBubble;
        readonly IAgentOrchestrator agentOrchestrator;

        public MainWindow(IAgentOrchestrator agentOrchestrator)
        {
            this.InitializeComponent();
            this.agentOrchestrator = agentOrchestrator;


            this.ExtendsContentIntoTitleBar = false;
            this.SetTitleBar(RootGrid);



            IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

            if (appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.SetBorderAndTitleBar(true, true);
                presenter.IsAlwaysOnTop = false;
                presenter.IsResizable = true; // Keep resizable ON
            }

            // ---FIX: DOCK THE WINDOW TO THE RIGHT SIDE OF THE SCREEN ON LAUNCH ---
            var displayArea = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(windowId, Microsoft.UI.Windowing.DisplayAreaFallback.Primary);
            int screenWidth = displayArea.WorkArea.Width;
            int screenHeight = displayArea.WorkArea.Height;

            int appWidth = 500;
            int appHeight = 1000;

            // Calculate right-aligned X coordinate
            int rightX = screenWidth - appWidth;
            int centerY = (screenHeight - appHeight) / 2;

            appWindow.MoveAndResize(new Windows.Graphics.RectInt32(rightX, centerY, appWidth, appHeight));

            // Monitor boundaries loop

            // NATIVELY STRIP AWAY MAXIMIZE CAPABILITIES ---
            int currentStyle = GetWindowLong(hwnd, GWL_STYLE);
            SetWindowLong(hwnd, GWL_STYLE, currentStyle & ~WS_MAXIMIZEBOX);

            // Initial window size launch boundaries
            ResizeWindow(appWidth, appHeight);

            // ENFORCE STRICT MAX/MIN RESIZE LIMITS ---
            appWindow.Changed += (sender, args) =>
            {
                if (args.DidSizeChange)
                {
                    int currentWidth = appWindow.Size.Width;
                    int currentHeight = appWindow.Size.Height;
                    bool needsResize = false;

                    // Define your maximum boundaries here (e.g., Max Width: 1000, Max Height: 800)
                    int maxWidth = 1000;
                    int maxHeight = 1200;
                    int minWidth = 520;
                    int minHeight = 400;

                    if (currentWidth > maxWidth) { currentWidth = maxWidth; needsResize = true; }
                    if (currentHeight > maxHeight) { currentHeight = maxHeight; needsResize = true; }
                    if (currentWidth < minWidth) { currentWidth = minWidth; needsResize = true; }
                    if (currentHeight < minHeight) { currentHeight = minHeight; needsResize = true; }

                    if (needsResize)
                    {
                        ResizeWindow(currentWidth, currentHeight);
                    }
                }

                if (args.DidPositionChange)
                {
                    AdaptToBackground(appWindow.Position.X, appWindow.Position.Y);
                }
            };

            colorDetectionTimer = new DispatcherTimer();
            colorDetectionTimer.Interval = TimeSpan.FromMilliseconds(400);
            colorDetectionTimer.Tick += (s, e) => AdaptToBackground(appWindow.Position.X, appWindow.Position.Y);
            colorDetectionTimer.Start();

            SetAppIcon(hwnd);
            RootGrid.Loaded += OnLoaded;
        }

        private void AdaptToBackground(int currentX, int currentY)
        {
            IntPtr hwnd = WindowNative.GetWindowHandle(this);
            bool isBrightBehindMe = ScreenColorDetector.IsBackgroundBright(currentX, currentY);

            if (isBrightBehindMe)
            {
                // 🟡 BRIGHT BACKGROUND MODE -> HIGH CONTRAST CYBERPUNK TINT
                int titlebarColor = unchecked((int)0x001A1009);  // Deep Indigo Title Frame (#09101A)
                int titleTextColor = unchecked((int)0x00FFFFFF); // White Title Text
                DwmSetWindowAttribute(hwnd, 35, ref titlebarColor, sizeof(int));
                DwmSetWindowAttribute(hwnd, 36, ref titleTextColor, sizeof(int));

                //if (SidebarPanel != null)
                //    SidebarPanel.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(220, 9, 10, 20));

                if (ChatWorkspacePanel != null)
                {
                    //ChatWorkspacePanel.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(180, 22, 18, 38));
                    ChatWorkspacePanel.Background = new SolidColorBrush(UIServices.ColorHelper.ToColor("000000")); // opaque black background
                    //ChatWorkspacePanel.BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(180, 139, 92, 246));
                    ChatWorkspacePanel.BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(180, 0, 0, 0));
                    ChatWorkspacePanel.BorderThickness = new Microsoft.UI.Xaml.Thickness(1.5);
                }
            }
            else
            {
                // 🔵 DEFAULT MODE -> THE REPLICATED REFERENCE IMAGE LOOK (DARK BACKGROUND)
                int titlebarColor = unchecked((int)0x002A170F);  // Dark Frame Blend
                int titleTextColor = unchecked((int)0x00FFFFFF); // White Title Text
                DwmSetWindowAttribute(hwnd, 35, ref titlebarColor, sizeof(int));
                DwmSetWindowAttribute(hwnd, 36, ref titleTextColor, sizeof(int));

                // Outer window shell tint (Replaces the old sidebar layout look)
                //if (SidebarPanel != null)
                //    SidebarPanel.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(160, 11, 8, 22));

                // Inner elevated chat card component
                if (ChatWorkspacePanel != null)
                {
                    //ChatWorkspacePanel.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(110, 18, 15, 36));
                    ChatWorkspacePanel.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 255, 255, 255)); // transparency background
                    //ChatWorkspacePanel.BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(120, 139, 92, 246));
                    ChatWorkspacePanel.BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 255, 255, 255)); // transparent border
                    ChatWorkspacePanel.BorderThickness = new Microsoft.UI.Xaml.Thickness(1.5);
                }
            }
        }

        private bool TrySetAcrylicBackdrop()
        {
            if (DesktopAcrylicController.IsSupported())
            {
                configurationSource = new SystemBackdropConfiguration();
                this.Activated += (s, e) => configurationSource.IsInputActive = true;

                this.Closed += (s, e) =>
                {
                    colorDetectionTimer?.Stop();
                    acrylicController?.Dispose();
                    acrylicController = null;
                };

                configurationSource.IsInputActive = true;
                // Keep the engine neutral so it doesn't default to an opaque backup brush
                configurationSource.Theme = SystemBackdropTheme.Default;

                acrylicController = new DesktopAcrylicController
                {
                    // FIX 2: Give the brush enough luminosity and tint to generate a beautiful glass refraction
                    TintOpacity = 0.15f,
                    TintColor = Windows.UI.Color.FromArgb(255, 15, 23, 42), // Deep Sci-Fi Slate base
                    LuminosityOpacity = 0.20f
                };

                acrylicController.AddSystemBackdropTarget(this.As<Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop>());
                acrylicController.SetSystemBackdropConfiguration(configurationSource);
                return true;
            }
            return false;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            IntPtr hwnd = WindowNative.GetWindowHandle(this);

            int borderThickness = 0;
            DwmSetWindowAttribute(hwnd, 37, ref borderThickness, sizeof(int));

            int transparentBorderColor = -2;
            DwmSetWindowAttribute(hwnd, 34, ref transparentBorderColor, sizeof(int));

            TrySetAcrylicBackdrop();
        }

        private void InputBox_PreviewKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                // Check the current state of the Shift key asynchronously via the system window thread
                var shiftState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift);
                bool isShiftPressed = (shiftState & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down;

                if (isShiftPressed)
                {
                    // 1. Shift + Enter -> Allow the native TextBox mechanism to append a new line string (\r\n)
                    e.Handled = false;
                }
                else
                {
                    // 2. Pure Enter -> Intercept the newline injection, run your submit routine instead
                    e.Handled = true; // Prevents the TextBox from inserting a blank line layout carriage return

                    Send_Click(this, new RoutedEventArgs());
                }
            }
        }

        private async void Send_Click(object sender, RoutedEventArgs e)
        {
            string input = InputBox.Text.Trim();
            if (string.IsNullOrEmpty(input)) return;

            InputBox.IsEnabled = SendButton.IsEnabled = false;
            InputBox.Text = String.Empty;

            Border userMessageBubble = new Border
            {
                // Smooth slate-purple background block for the message bubble container
                //Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 124, 58, 237)), // Solid Purple Accent
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 0, 0)), // Solid Purple Accent
                CornerRadius = new CornerRadius(12, 12, 0, 12), // Tailored speech bubble look
                Padding = new Thickness(14, 10, 14, 10),
                Margin = new Thickness(24, 4, 4, 4), // Pushes the user bubble to the right side
                HorizontalAlignment = HorizontalAlignment.Right,
                Child = new TextBlock
                {
                    Text = "You: " + input,
                    Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
                    FontSize = 14,
                    TextWrapping = TextWrapping.Wrap // Ensures long entries wrap downwards cleanly
                }
            };

            ChatPanel.Children.Add(userMessageBubble);

            // AI Thinking message
            thinkingText = new TextBlock
            {
                Text = "AI: Thinking",
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
                FontSize = 14
            };
            thinkingBubble = new Border
            {
                Background = new SolidColorBrush(
            Microsoft.UI.ColorHelper.FromArgb(80, 139, 92, 246)),

                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10),
                Margin = new Thickness(4),
                HorizontalAlignment = HorizontalAlignment.Left,
                Child = thinkingText
            };

            ChatPanel.Children.Add(thinkingBubble);
            try
            {
                await CallAIAgent(input);
            }
            finally
            {
                ChatPanel.Children.Remove(thinkingBubble);
                InputBox.IsEnabled = SendButton.IsEnabled = true;
            }
        }

        private async Task CallAIAgent(string input)
        {
            StartThinkingAnimation();
            currentActiveTaskBlock = null;
            cts?.Dispose();
            cts = new CancellationTokenSource();

            var progress = new Progress<GoalProgress>(update =>
            {
                thinkingText.Text = "AI: Processing";
                HandleGoalProgress(update);
            });

            UserPromptInput userPromptInput = new UserPromptInput() { Query = input, FileName = "goal_request.txt" };
            userPromptInput.IsTask = false;

            Plan aiResponse;
            try
            {
                aiResponse = await agentOrchestrator.Run(userPromptInput, progress, cts.Token);
            }
            catch (OperationCanceledException)
            {
                thinkingText.Text = "AI: Cancelled";
                return;
            }
            finally
            {
                StopThinkingAnimation();
            }
        }

        private void OnCancelRequested(int stepId)
        {
            if (cts == null) return;

            cts.Cancel();
            cts.Dispose();
            cts = null;
        }

        private async void OnRestartRequested(int stepId)
        {
            if (cts != null)
            {
                cts.Cancel();
                cts.Dispose();
            }

            cts = new CancellationTokenSource();

            var progress = new Progress<GoalProgress>(update =>
            {
                thinkingText.Text = "AI: Processing";
                HandleGoalProgress(update);
            });

            await agentOrchestrator.Restart(stepId, progress, cts.Token);
        }

        private void HandleGoalProgress(GoalProgress update)
        {
            if (currentActiveTaskBlock == null)
            {
                currentActiveTaskBlock = new TaskMessageBlock(update.Goal);
                currentActiveTaskBlock.RestartTaskRequested += OnRestartRequested;
                currentActiveTaskBlock.CancelTaskRequested += OnCancelRequested;
                ChatPanel.Children.Add(currentActiveTaskBlock.RenderedElement);
            }

            string stepId = string.IsNullOrWhiteSpace(update.StepKey)
                ? update.StepId.ToString()
                : update.StepKey;

            if (!string.IsNullOrWhiteSpace(update.StepObjective))
            {
                currentActiveTaskBlock.AddTask(Convert.ToInt32(stepId), update.StepId + ": " + update.StepObjective);
            }

            currentActiveTaskBlock.UpdateTaskState(Convert.ToInt32(stepId), update.Status);
        }

        private void SetAppIcon(IntPtr hwnd)
        {
            try
            {
                // 1. Point this string directly to the physical location of your icon asset file.
                // Make sure the file's build action property in Visual Studio is set to "Content".
                string iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");

                // 2. Load the image sizes natively (16x16 pixels for the small title bar area slot)
                IntPtr hIconSmall = LoadImage(IntPtr.Zero, iconPath, IMAGE_ICON, 16, 16, LR_LOADFROMFILE);
                IntPtr hIconLarge = LoadImage(IntPtr.Zero, iconPath, IMAGE_ICON, 32, 32, LR_LOADFROMFILE);

                if (hIconSmall != IntPtr.Zero)
                {
                    // Send the OS messaging handles telling it to override the default generic application window flag
                    SendMessage(hwnd, WM_SETICON, (IntPtr)0, hIconSmall); // 0 = ICON_SMALL
                    SendMessage(hwnd, WM_SETICON, (IntPtr)1, hIconLarge); // 1 = ICON_BIG
                }
            }
            catch (Exception)
            {
                // Fail-safe block to keep the application executing normally if the asset file is missing
            }
        }

        private void ResizeWindow(int width, int height)
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

            appWindow.Resize(new Windows.Graphics.SizeInt32(width, height));
        }

        private void StartThinkingAnimation()
        {
            int dots = 0;

            thinkingTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };

            thinkingTimer.Tick += (s, e) =>
            {
                dots = (dots + 1) % 4;

                thinkingText.Text = "AI: Processing" + new string('.', dots);
            };

            thinkingTimer.Start();
        }

        private void StopThinkingAnimation()
        {
            thinkingTimer?.Stop();
            thinkingTimer = null;
        }
    }
}