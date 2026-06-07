using Microsoft.Playwright;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AgenticBrowserAI.Helpers
{
    public sealed class BrowserObserver
    {
        private readonly IBrowserContext context;
        private readonly SemaphoreSlim gate = new(1, 1);
        private readonly ActivePageResolver resolver;

        public BrowserObserver(IPage initialPage)
        {
            context = initialPage.Context;
            resolver = new ActivePageResolver(context);
        }

        public IPage ActivePage => resolver.GetActivePage();

        public async Task<IPage> WaitForStableActivePage(TimeSpan timeout)
        {
            return await resolver.WaitForStableActivePage(timeout);
        }

        public async Task<string> ObserveDomAsync(TimeSpan timeout, bool waitForUserContinue = false)
        {
            await gate.WaitAsync();

            try
            {
                if (waitForUserContinue)
                {
                    await WaitForUserContinue(timeout);
                }

                var page = await WaitForStableActivePage(timeout);
                var watcher = new PlaywrightDOMWatcher(page);
                return await watcher.GetDomHtml();
            }
            finally
            {
                gate.Release();
            }
        }

        private async Task WaitForUserContinue(TimeSpan timeout)
        {
            using var cts = new CancellationTokenSource(timeout);

            while (!cts.IsCancellationRequested)
            {
                IPage page;

                try
                {
                    page = await WaitForStableActivePage(TimeSpan.FromSeconds(10));
                }
                catch (TimeoutException)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(page.Url) || page.Url.StartsWith("about:"))
                    return;

                for (int i = 0; i < 5; i++)
                {
                    await TryInjectContinueButton(page);

                    var exists = await TryEvaluate(page,
                        "() => !!document.getElementById('__agent_continue_button')", false);

                    if (exists) break;

                    await Task.Delay(300);
                }

                bool clicked = await TryEvaluate(page, "() => window.__agentContinueAutomation === true", false);
                if (clicked)
                {
                    await TryEvaluate(page, "() => { delete window.__agentContinueAutomation; document.getElementById('__agent_continue_button')?.remove(); }", false);
                    return;
                }

                try
                {
                    await Task.Delay(500, cts.Token);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }

            throw new TimeoutException("Timed out waiting for the user to continue automation.");
        }

        private static async Task TryInjectContinueButton(IPage page)
        {
            for (int i = 0; i < 3; i++)
            {
                try
                {
                    await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

                    // wait for body (fixes most failures)
                    await page.WaitForSelectorAsync("body", new() { Timeout = 5000 });

                    await page.EvaluateAsync(@"() => {
            if (document.getElementById('__agent_continue_button')) return;

            const btn = document.createElement('button');
            btn.id = '__agent_continue_button';
            btn.innerText = 'Continue Automation';
            btn.style.position = 'fixed';
            btn.style.top = '10px';
            btn.style.right = '10px';
            btn.style.zIndex = 2147483647;
            btn.style.padding = '10px 14px';
            btn.style.border = '0';
            btn.style.borderRadius = '8px';
            btn.style.background = '#FF0000';
            btn.style.color = '#fff';
            btn.style.font = '600 13px Segoe UI, sans-serif';
            btn.style.boxShadow = '0 8px 24px rgba(0,0,0,.24)';
            btn.onclick = () => {
                window.__agentContinueAutomation = true;
                btn.remove();
            };

            document.body.appendChild(btn);
        }");
                }
                catch (Exception ex)
                {
                    FileLogger.Log($"Inject button failed: {ex.Message}");
                    await Task.Delay(300);
                }
            }
        }

        private static async Task<T> TryEvaluate<T>(IPage page, string script, T fallback)
        {
            try
            {
                return await page.EvaluateAsync<T>(script);
            }
            catch
            {
                return fallback;
            }
        }
    }
}
