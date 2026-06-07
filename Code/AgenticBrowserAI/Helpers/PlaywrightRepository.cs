using Microsoft.Playwright;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AgenticBrowserAI.Helpers;

namespace AgenticBrowserAI.Helpers
{
    public class PlaywrightRepository : IPlaywrightRepository, IAsyncDisposable
    {
        private readonly ConcurrentDictionary<string, IBrowserContext> goalContexts = new();
        private readonly ConcurrentDictionary<IBrowserContext, SemaphoreSlim> storageStateLocks = new();
        private readonly SemaphoreSlim createContextGate = new(1, 1);
        private readonly Task initializeTask;
        private readonly string storageStatePath;

        private IPlaywright? playwright;
        private IBrowser? browser;

        public PlaywrightRepository()
        {
            storageStatePath = Path.Combine(AppContext.BaseDirectory, "auth.json");
            initializeTask = InitializeAsync();
        }

        public async Task InitializeAsync()
        {
            playwright = await Playwright.CreateAsync();
            browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Channel = "msedge",
                Headless = false
            });
        }

        public async Task<IPage> CreatePage(string goalId)
        {
            await initializeTask;

            if (browser == null)
            {
                throw new InvalidOperationException("Playwright browser was not initialized.");
            }
            
            var context = await GetOrCreateContext(goalId);
            var page = await context.NewPageAsync();
            AttachPageListeners(page, context);
            FileLogger.Log(await page.EvaluateAsync<string>("navigator.userAgent"));
            return page;
        }

        private async Task<IBrowserContext> GetOrCreateContext(string goalId)
        {
            if (goalContexts.TryGetValue(goalId, out var existingContext))
            {
                return existingContext;
            }

            await createContextGate.WaitAsync();

            try
            {
                if (goalContexts.TryGetValue(goalId, out existingContext))
                {
                    return existingContext;
                }

                var context = await CreateBrowserContext();
                goalContexts[goalId] = context;
                storageStateLocks.TryAdd(context, new SemaphoreSlim(1, 1));
                AttachContextListeners(context);

                return context;
            }
            finally
            {
                createContextGate.Release();
            }
        }

        private async Task<IBrowserContext> CreateBrowserContext()
        {
            if (browser == null)
            {
                throw new InvalidOperationException("Playwright browser was not initialized.");
            }

            if (File.Exists(storageStatePath))
            {
                try
                {
                    return await browser.NewContextAsync(new BrowserNewContextOptions
                    {
                        StorageStatePath = storageStatePath
                    });
                }
                catch (Exception ex)
                {
                    FileLogger.Log($"Failed to load storage state '{storageStatePath}'. Starting clean context. {ex.Message}");
                }
            }

            return await browser.NewContextAsync();
        }

        public async Task SaveStorageStateAsync(string goalId)
        {
            if (goalContexts.TryGetValue(goalId, out var context))
            {
                await SaveStorageStateAsync(context);
            }
        }

        public async Task CloseGoalAsync(string goalId)
        {
            if (goalContexts.TryRemove(goalId, out var context))
            {
                await SaveStorageStateAsync(context);
                await context.CloseAsync();
            }
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var context in goalContexts.Values)
            {
                await SaveStorageStateAsync(context);
                await context.CloseAsync();
            }

            if (browser != null)
            {
                await browser.DisposeAsync();
            }

            playwright?.Dispose();
        }

        private void AttachContextListeners(IBrowserContext context)
        {
            context.Page += (_, page) =>
            {
                FileLogger.Log($"New page opened: {page.Url}");
                AttachPageListeners(page, context);
            };
        }

        private void AttachPageListeners(IPage page, IBrowserContext context)
        {
            page.FrameNavigated += (_, frame) =>
            {
                if (frame == page.MainFrame)
                {
                    FileLogger.Log($"Navigated: {frame.Url}");
                    _ = SafeSaveStorageStateAsync(context, page);

                }
            };

            page.Load += (_, _) =>
            {
                FileLogger.Log($"Loaded: {page.Url}");
                _ = SafeSaveStorageStateAsync(context, page);

            };
        }

        private async Task SafeSaveStorageStateAsync(IBrowserContext context, IPage page)
        {
            try
            {
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 5000 });
            }
            catch { }

            try
            {
                await SaveStorageStateAsync(context);
            }
            catch { }
        }

        private async Task SaveStorageStateAsync(IBrowserContext context)
        {
            if (!storageStateLocks.TryGetValue(context, out var gate))
            {
                gate = storageStateLocks.GetOrAdd(context, _ => new SemaphoreSlim(1, 1));
            }

            if (!await gate.WaitAsync(0))
            {
                return; // skip if already saving
            }

            try
            {
                var directory = Path.GetDirectoryName(storageStatePath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                await context.StorageStateAsync(new BrowserContextStorageStateOptions
                {
                    Path = storageStatePath
                });
            }
            catch (Exception ex)
            {
                FileLogger.Log($"Failed to save storage state '{storageStatePath}'. {ex.Message}");
            }
            finally
            {
                gate.Release();
            }
        }
    }

    public interface IPlaywrightRepository
    {
        Task<IPage> CreatePage(string goalId);
        Task SaveStorageStateAsync(string goalId);
    }
}
