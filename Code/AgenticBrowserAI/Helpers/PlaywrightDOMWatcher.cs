using Microsoft.Playwright;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgenticBrowserAI.Helpers
{
    public class PlaywrightDOMWatcher
    {
        readonly IPage page;

        public PlaywrightDOMWatcher(IPage page)
        {
            this.page = page;
        }

        public async Task<string> GetDomHtml(int stableMs = 800, int timeoutMs = 3000)
        {
            // 1. Let JS start rendering updates
            await page.WaitForTimeoutAsync(100);

            // 2. Wait for UI anchors (if any exist in your app)
            try
            {
                await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
            }
            catch { }

            // 3. DOM mutation stability (CORE)
            await WaitForDomStability(stableMs);

            // 4. Final micro-buffer (important for React commits)
            await page.WaitForTimeoutAsync(1000);

            // 5. Pass the updated DOM
            return await page.ContentAsync();
        }

        public async Task WaitForDomStability(int stableMs = 500)
        {
            try
            {
                await page.EvaluateAsync($@"
        () => new Promise(resolve => {{
            let timer;

            const observer = new MutationObserver(() => {{
                clearTimeout(timer);
                timer = setTimeout(() => {{
                    observer.disconnect();
                    resolve();
                }}, {stableMs});
            }});

            observer.observe(document.body, {{
                subtree: true,
                childList: true,
                attributes: true
            }});

            timer = setTimeout(() => {{
                observer.disconnect();
                resolve();
            }}, {stableMs});
        }});
    ");
            }
            catch (Exception e) { }
        }
    }
}