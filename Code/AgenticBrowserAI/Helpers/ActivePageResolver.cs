using Microsoft.Playwright;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AgenticBrowserAI.Helpers
{
    internal sealed class PageState
    {
        public IPage Page { get; init; } = default!;
        public DateTime LastEventUtc { get; set; } = DateTime.UtcNow;
        public DateTime LastInteractionUtc { get; set; } = DateTime.UtcNow;
        public DateTime LastNavigationUtc { get; set; } = DateTime.UtcNow;

        public bool IsPopup { get; set; }
        public bool IsFocused { get; set; }
        public bool IsClosed => Page.IsClosed;

        public string Url => Page.Url ?? string.Empty;
    }

    public sealed class ActivePageResolver
    {
        private readonly IBrowserContext context;
        private readonly object sync = new();

        private readonly Dictionary<IPage, PageState> states = new();

        public ActivePageResolver(IBrowserContext context)
        {
            this.context = context;

            foreach (var page in context.Pages)
                Track(page);

            context.Page += (_, page) => Track(page);
        }

        public IPage? GetActivePage()
        {
            lock (sync)
            {
                var candidates = states.Values
                    .Where(s => !s.IsClosed)
                    .Where(IsValid)
                    .ToList();

                if (!candidates.Any())
                    return null;

                var best = candidates
                    .OrderByDescending(Score)
                    .First();

                return best.Page;
            }
        }

        public async Task<IPage> WaitForStableActivePage(TimeSpan timeout)
        {
            using var cts = new CancellationTokenSource(timeout);

            while (!cts.IsCancellationRequested)
            {
                var page = GetActivePage();

                if (page != null && !page.IsClosed)
                {
                    try
                    {
                        await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded, new() { Timeout = 2000 });
                    }
                    catch { }

                    try
                    {
                        await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 2000 });
                    }
                    catch { }

                    if (page == GetActivePage())
                        return page;
                }

                await Task.Delay(200, cts.Token);
            }

            throw new TimeoutException("No stable active page found.");
        }

        private void Track(IPage page)
        {
            var state = new PageState { Page = page };

            lock (sync)
            {
                if (states.ContainsKey(page)) return;
                states[page] = state;
            }

            page.DOMContentLoaded += (_, _) =>
            {
                state.LastNavigationUtc = DateTime.UtcNow;
                state.LastEventUtc = DateTime.UtcNow;
            };

            page.Load += (_, _) =>
            {
                state.LastNavigationUtc = DateTime.UtcNow;
                state.LastEventUtc = DateTime.UtcNow;
            };

            page.FrameNavigated += (_, frame) =>
            {
                if (frame == page.MainFrame)
                {
                    state.LastNavigationUtc = DateTime.UtcNow;
                    state.LastEventUtc = DateTime.UtcNow;
                }
            };

            page.Popup += (_, popup) =>
            {
                Track(popup);

                lock (sync)
                {
                    states[popup].IsPopup = true;
                    states[popup].LastInteractionUtc = DateTime.UtcNow;
                }
            };

            page.Close += (_, _) =>
            {
                lock (sync)
                {
                    states.Remove(page);
                }
            };

            _ = InjectInteractionTracking(page, state);
        }

        private int Score(PageState s)
        {
            int score = 0;

            if (string.IsNullOrWhiteSpace(s.Url)) return -1000;
            if (s.Url.StartsWith("about:")) return -1000;

            if (s.IsFocused) score += 1000;

            score += ScoreTime(s.LastInteractionUtc, 800, 500);
            score += ScoreTime(s.LastNavigationUtc, 1500, 300);
            score += ScoreTime(s.LastEventUtc, 2000, 100);

            if (s.IsPopup) score += 200;

            return score;
        }

        private static int ScoreTime(DateTime time, int windowMs, int maxScore)
        {
            var delta = (DateTime.UtcNow - time).TotalMilliseconds;
            if (delta > windowMs) return 0;

            return (int)(maxScore * (1 - (delta / windowMs)));
        }

        private static bool IsValid(PageState s)
        {
            if (s.IsClosed) return false;
            if (string.IsNullOrWhiteSpace(s.Url)) return false;
            if (s.Url.StartsWith("about:")) return false;

            return true;
        }

        private async Task InjectInteractionTracking(IPage page, PageState state)
        {
            try
            {
                await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

                await page.ExposeBindingAsync("__notifyInteraction", (source, args) =>
                {
                    state.LastInteractionUtc = DateTime.UtcNow;
                    state.IsFocused = true;
                    return Task.CompletedTask;
                });

                await page.EvaluateAsync(@"() => {
                const notify = () => window.__notifyInteraction?.();

                window.addEventListener('focus', notify, true);
                window.addEventListener('click', notify, true);
                window.addEventListener('keydown', notify, true);
            }");
            }
            catch { }
        }
    }
}
