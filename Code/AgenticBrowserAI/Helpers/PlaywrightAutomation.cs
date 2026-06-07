using AgenticBrowserAI.Helpers.Security;
using AgenticBrowserAI.Models;
using Azure;
using HtmlAgilityPack;
using Microsoft.Playwright;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace AgenticBrowserAI.Helpers
{
    public class PlaywrightAutomation : IPlaywrightAutomation
    {
        readonly BrowserObserver browserObserver;
        IPage page;
        PlaywrightDOMWatcher watcher;

        public PlaywrightAutomation(IPage page)
        {
            this.page = page;
            this.browserObserver = new BrowserObserver(page);
            this.watcher = new PlaywrightDOMWatcher(page);
        }

        public async Task Wait(int milliseconds)
        {
            await watcher.WaitForDomStability();
            await browserObserver.ActivePage.WaitForTimeoutAsync(milliseconds);
        }

        public async Task PressKey(string key)
        {
            await browserObserver.ActivePage.Keyboard.PressAsync(key);
            await watcher.WaitForDomStability();
        }

        public async Task Fill(string locator, string value, IEnumerable<ILocator>? searchInLocators = null)
        {
            if (string.IsNullOrWhiteSpace(locator)) throw new ArgumentException("Locator cannot be empty");

            if (searchInLocators != null && searchInLocators.Any())
            {
                await searchInLocators.First().FillAsync(value);
                return;
            }

            await browserObserver.ActivePage.Locator(locator).FillAsync(value);
        }

        public async Task<bool> Navigate(string url)
        {
            SecurityResult? securityResult = VerifyUrl.Check(url);

            if (securityResult != null && !securityResult.IsMalicious)
            {
                var result = page.GotoAsync(url);
                await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
                await page.WaitForTimeoutAsync(2000);
                await watcher.WaitForDomStability();
                return result.IsCompletedSuccessfully;
            }

            return false;
        }

        public async Task<IPage?> OpenNewTab(string url)
        {
            SecurityResult? securityResult = VerifyUrl.Check(url);

            if (securityResult != null && !securityResult.IsMalicious)
            {
                try
                {
                    var newTabRef = await browserObserver.ActivePage.Context.NewPageAsync();
                    await newTabRef.GotoAsync(url);
                    return newTabRef;
                }
                catch (Exception ex)
                {
                }
            }

            return null;
        }

        public async Task<IReadOnlyList<ILocator>> Loop(string locators)
        {
            if (string.IsNullOrWhiteSpace(locators))
                throw new ArgumentException("Locators cannot be empty");

            var unique = new Dictionary<string, ILocator>();

            var searchLocators = SplitInput(locators);

            foreach (var loc in searchLocators)
            {
                var candidates = browserObserver.ActivePage.Locator(loc);
                var count = await candidates.CountAsync();

                if (count > 0)
                {
                    for (int i = 0; i < count; i++)
                    {
                        var element = candidates.Nth(i);

                        var id = await element.GetAttributeAsync("id");

                        if (!unique.ContainsKey(id))
                        {
                            var newLocator = browserObserver.ActivePage.Locator("[id =\"" + id + "\"]");
                            var c = await newLocator.CountAsync();
                            unique[id] = newLocator;
                        }
                    }
                }
            }

            return unique.Select(a => a.Value).ToList();
        }

        public async Task<IReadOnlyList<ILocator>> Loop(string locators, string keywords)
        {
            if (string.IsNullOrWhiteSpace(locators) && string.IsNullOrWhiteSpace(keywords))
                throw new ArgumentException("Locator and Keywords cannot be empty");

            var unique = new Dictionary<string, ILocator>();

            var foundLocator = await Find(locators, keywords);

            var results = new List<ILocator>();

            if (foundLocator == null)
                return results;

            var count = await foundLocator.CountAsync();

            for (int i = 0; i < count; i++)
            {
                var element = foundLocator.Nth(i);

                var id = await element.GetAttributeAsync("id");

                if (!unique.ContainsKey(id))
                {
                    var newLocator = browserObserver.ActivePage.Locator("[id =\"" + id + "\"]");
                    var c = await newLocator.CountAsync();
                    unique[id] = newLocator;
                }
            }

            return unique.Select(a => a.Value).ToList();
        }

        public async Task<ILocator?> Find(string locators, string keywords, IEnumerable<ILocator>? searchInLocators = null)
        {
            await this.browserObserver.WaitForStableActivePage(TimeSpan.FromSeconds(30));
            var locatorList = SplitInput(locators);
            var keywordList = SplitInput(keywords);
            ILocator? finalLocator = null;

            if (searchInLocators != null)
            {
                foreach (var searchInLocator in searchInLocators)
                {
                    finalLocator = await findInLocator(locatorList, keywordList, searchInLocator);
                }
            }
            else
            {
                finalLocator = await findInLocator(locatorList, keywordList);
            }

            if (finalLocator == null)
                return null;

            var count = await finalLocator.CountAsync();

            var s = await finalLocator.AllAsync();
            foreach (var item in s)
            {
                var c = await item.CountAsync();
                await DebugLocator(locators, item, c);
            }

            return count > 0 ? finalLocator : null;
        }

        private async Task<ILocator?> findInLocator(string[] locatorList, string[] keywordList, ILocator? searchInLocator = null)
        {
            ILocator? finalLocator = null;

            foreach (var loc in locatorList)
            {
                var candidates = (searchInLocator != null ? searchInLocator.Locator(loc) : browserObserver.ActivePage.Locator(loc));
                var count1 = await candidates.CountAsync();

                await DebugLocator(loc, candidates, count1);

                var matchingItems = await GetMatchingItems(candidates, keywordList, "OR");

                foreach (var item in matchingItems)
                {
                    finalLocator = finalLocator == null
                        ? item
                        : finalLocator.Or(item);
                }
            }

            return finalLocator;
        }

        public async Task<IReadOnlyList<ILocator>> Filter(string locators, string keywords, string condition, ILocator? searchInLocator = null)
        {
            var locatorList = SplitInput(locators);
            var keywordList = SplitInput(keywords);

            var results = new List<ILocator>();

            foreach (var loc in locatorList)
            {
                ILocator? candidates = null;

                if (searchInLocator != null)
                {
                    candidates = searchInLocator.Locator(loc);
                }
                else
                {
                    candidates = browserObserver.ActivePage.Locator(loc);
                }

                results.AddRange(await GetMatchingItems(candidates, keywordList, condition));
            }

            return results;
        }

        public async Task<KeyValuePair<string, IPage?>?> ClickOpenTab(string? locator = null, IEnumerable<ILocator>? searchInLocators = null)
        {
            if (searchInLocators != null && searchInLocators.Any())
            {
                //var href = await searchInLocators.First().Nth(0).GetAttributeAsync("href");
                //if (href != null)
                //{
                //    SecurityResult? securityResult = VerifyUrl.Check(href);
                //    if (securityResult != null && !securityResult.IsMalicious)
                //    {
                //        var hrefRef = await OpenNewTab(href);
                //        return new KeyValuePair<string, IPage>(href, hrefRef);
                //    }
                //}

                var filteredLocator = await Find(locator, null, searchInLocators);

                if (filteredLocator != null && !String.IsNullOrWhiteSpace(locator))
                {
                    var countFilteredLoc = await filteredLocator.CountAsync();

                    if (countFilteredLoc > 0)
                    {
                        for (int i = 0; i < countFilteredLoc; i++)
                        {
                            var clickable = filteredLocator.Nth(i).Locator("xpath=ancestor-or-self::a | ancestor-or-self::button").First;
                            if (clickable != null)
                            {
                                var count = await clickable.CountAsync();
                                if (count > 0)
                                {
                                    var href = await clickable.GetAttributeAsync("href");
                                    if (!String.IsNullOrWhiteSpace(href))
                                    {
                                        var hrefRef = await OpenNewTab(href);
                                        return new KeyValuePair<string, IPage?>(href, hrefRef);
                                    }
                                }
                                else
                                {
                                    var html = await filteredLocator.Nth(0).InnerHTMLAsync();
                                    var hrefLink = GetLink(html);
                                    if (!String.IsNullOrWhiteSpace(hrefLink))
                                    {
                                        var hrefRef = await OpenNewTab(hrefLink);
                                        return new KeyValuePair<string, IPage?>(hrefLink, hrefRef);
                                    }
                                }
                            }
                        }
                    }
                }

                //var anchorTag = clickLocator?.Locator("a");
                //if (anchorTag != null)
                //{
                //    var countElements = await anchorTag.CountAsync();
                //    if (countElements > 0)
                //    {
                //        var hrefLink = await anchorTag.GetAttributeAsync("href");
                //        if (!String.IsNullOrWhiteSpace(hrefLink))
                //        {
                //            SecurityResult? securityResult = VerifyUrl.Check(hrefLink);

                //            if (securityResult != null && !securityResult.IsMalicious)
                //            {
                //                return await OpenTab(hrefLink);
                //            }
                //        }
                //    }
                //}
            }
            else if (!String.IsNullOrWhiteSpace(locator))
            {
                var findLocator = await Find(locator, null);

                if (findLocator != null)
                {
                    var count = await findLocator.CountAsync();

                    if (count > 0)
                    {
                        var anchorTag = findLocator?.Nth(0).Locator("xpath=ancestor-or-self::a | ancestor-or-self::button").First;
                        if (anchorTag != null)
                        {
                            var countElements = await anchorTag.CountAsync();
                            if (countElements > 0)
                            {
                                var hrefLink = await anchorTag.Nth(0).GetAttributeAsync("href");
                                if (!String.IsNullOrWhiteSpace(hrefLink))
                                {
                                    SecurityResult? securityResult = VerifyUrl.Check(hrefLink);

                                    if (securityResult != null && !securityResult.IsMalicious)
                                    {
                                        var hrefRef = await OpenNewTab(hrefLink);
                                        return new KeyValuePair<string, IPage>(hrefLink, hrefRef);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return null;
        }

        public async Task<bool> CloseNewTab(string? locator = null)
        {
            bool result = true;

            try
            {
                await browserObserver.ActivePage.CloseAsync();
            }
            catch (Exception ex)
            {
                FileLogger.Log("Error closing tab: " + ex.Message);
                result = false;
            }

            return result;
        }

        public async Task<bool> Click(string? locator = null, IEnumerable<ILocator>? clickThisLocators = null)
        {
            if (clickThisLocators != null && clickThisLocators.Any())
            {
                var clickLocator = clickThisLocators.First().Nth(0);

                var anchorTag = clickLocator.Locator("a");
                if (anchorTag != null)
                {
                    var countElements = await anchorTag.CountAsync();
                    if (countElements > 0)
                    {
                        var hrefLink = await anchorTag.GetAttributeAsync("href");
                        if (!String.IsNullOrWhiteSpace(hrefLink))
                        {
                            return await Navigate(hrefLink);
                        }
                    }
                }

                await clickLocator.ScrollIntoViewIfNeededAsync();
                await clickLocator.ClickAsync();
            }
            else if (!String.IsNullOrWhiteSpace(locator))
            {
                var findLocator = await Find(locator, null);

                if (findLocator != null)
                {
                    var count = await findLocator.CountAsync();

                    if (count > 0)
                    {
                        await findLocator.Nth(0).ClickAsync();
                        await watcher.WaitForDomStability();
                        return true;
                    }
                }
            }

            return false;
        }

        public async Task Click(string locator, int index)
        {
            if (string.IsNullOrWhiteSpace(locator)) throw new ArgumentException("Locator cannot be empty");
            if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));

            await browserObserver.ActivePage.Locator(locator).Nth(index).ClickAsync();
        }

        //        public async Task<string> Observe()
        //        {
        //            try
        //            {
        //                await Task.Delay(30000);
        //                var tcs = new TaskCompletionSource<bool>();

        //                // 1. Expose function to browser
        //                await page.ExposeFunctionAsync("notifyDone", () =>
        //                {
        //                    tcs.TrySetResult(true);
        //                    return Task.CompletedTask;
        //                });

        //                // 2. Inject button on EVERY navigation
        //                await page.AddInitScriptAsync(@"() => {
        //    if (window.__pw_continue_btn__) return;

        //    function addButton() {
        //        const btn = document.createElement('button');
        //        btn.innerText = 'Continue Automation';
        //        btn.style.position = 'fixed';
        //        btn.style.top = '10px';
        //        btn.style.right = '10px';
        //        btn.style.zIndex = 999999;
        //        btn.style.padding = '10px';
        //        btn.style.background = 'red';
        //        btn.style.color = 'white';

        //        btn.onclick = () => {
        //            if (window.notifyDone) {
        //                window.notifyDone();
        //            }
        //            btn.remove();
        //        };

        //        document.body.appendChild(btn);
        //        window.__pw_continue_btn__ = true;
        //    }

        //    if (document.readyState === 'loading') {
        //        document.addEventListener('DOMContentLoaded', addButton);
        //    } else {
        //        addButton();
        //    }
        //}");

        //                // 3. Wait for user to click button
        //                await tcs.Task;

        //                // 4. Optional: small stabilization (no blind delays)
        //                await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        //                // 5. Return final DOM
        //                return await page.ContentAsync();
        //            }
        //            catch (Exception ex)
        //            {
        //                return null;
        //            }
        //        }

        public async Task<string> Observe()
        {
            string s = await browserObserver.ObserveDomAsync(TimeSpan.FromMinutes(5));
            return s;
        }

        public async Task<List<string>> GetHtmlList(ILocator locator)
        {
            var results = new List<string>();
            var count = await locator.CountAsync();

            for (int i = 0; i < count; i++)
            {
                var element = locator.Nth(i);
                var html = await element.InnerHTMLAsync();
                results.Add(html);
            }

            return results;
        }

        public async Task<List<string>> GetOuterHtmlList(ILocator locator)
        {
            var results = new List<string>();
            var count = await locator.CountAsync();

            for (int i = 0; i < count; i++)
            {
                var element = locator.Nth(i);

                var html = await element.EvaluateAsync<string>(
                    "el => el.outerHTML"
                );

                results.Add(html);
            }

            return results;
        }

        #region Private methods

        private static async Task DebugLocator(string locators, ILocator item, int count)
        {
            for (int i = 0; i < count; i++)
            {
                var h = await item.Nth(i).InnerHTMLAsync();
                var a = await item.Nth(i).AllInnerTextsAsync();
                FileLogger.Log(count + " LOCATORS " + locators + ": \r\n\r\nAllInnerTextsAsync: " + a + "\r\n\r\n INNERHTML: " + h);
            }
        }

        private string[] SplitInput(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return Array.Empty<string>();
            }

            return input.Split("||", StringSplitOptions.RemoveEmptyEntries)
                        .Select(x => x.Trim())
                        .ToArray();
        }

        private static async Task<IReadOnlyList<ILocator>> GetMatchingItems2(ILocator candidates, IReadOnlyList<string> keywords, string condition)
        {
            if (keywords == null || keywords.Count == 0)
            {
                return new List<ILocator> { candidates };
            }

            var normalized = keywords
                .Where(k => !string.IsNullOrWhiteSpace(k))
                .Select(k => k.Trim().ToLowerInvariant())
                .ToList();

            ILocator filtered;

            if (condition.Equals("AND", StringComparison.OrdinalIgnoreCase))
            {
                filtered = candidates;
                foreach (var keyword in normalized)
                {
                    filtered = filtered.Filter(new() { HasTextString = keyword });
                }
            }
            else
            {
                var selector = string.Join(", ",
                    normalized.Select(k => $":scope:has-text(\"{k}\")"));

                filtered = candidates.Locator(selector);
            }

            var count = await filtered.CountAsync();

            if (count == 0)
            {
                // fallback
                return new List<ILocator> { candidates.First };
            }

            // return each matched element as locator
            var results = new List<ILocator>();
            for (int i = 0; i < count; i++)
            {
                results.Add(filtered.Nth(i));
            }

            return results;
        }

        private static async Task<IReadOnlyList<ILocator>> GetMatchingItems(ILocator candidates, IReadOnlyList<string> keywords, string condition)
        {
            var results = new List<ILocator>();
            var count = await candidates.CountAsync();

            for (int i = 0; i < count; i++)
            {
                var element = candidates.Nth(i);
                var h = await element.InnerHTMLAsync();
                FileLogger.Log("GetMatchingItems: " + h);
                if (keywords == null || keywords.Count == 0)
                {
                    results.Add(element);
                    continue;
                }

                var text = await GetSearchableText(element);

                bool match = condition.Equals("AND", StringComparison.OrdinalIgnoreCase)
                    ? keywords.All(keyword => ContainsKeyword(text, keyword))
                    : keywords.Any(keyword => ContainsKeyword(text, keyword));

                if (match)
                {
                    results.Add(element);
                }
            }

            if (!results.Any() && count > 0)
            {
                results.Add(candidates);
            }

            return results;
        }

        private static async Task<string> GetSearchableText(ILocator element)
        {
            try
            {
                return await element.InnerTextAsync();
            }
            catch
            {
                return await element.TextContentAsync() ?? string.Empty;
            }
        }

        private static bool ContainsKeyword(string text, string keyword)
        {
            return !string.IsNullOrWhiteSpace(keyword)
                && text.Contains(keyword.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private string GetLink(string html)
        {
            var doc = new HtmlAgilityPack.HtmlDocument();
            doc.LoadHtml(html);

            var anchors = doc.DocumentNode.SelectNodes("//a[@href]");

            var keywords = new[] { "opt out", "unsubscribe", "click here" };

            var results = new List<string>();

            foreach (var a in anchors ?? Enumerable.Empty<HtmlNode>())
            {
                var text = a.InnerText?.ToLowerInvariant() ?? "";

                if (keywords.Any(k => text.Contains(k)))
                {
                    var href = a.GetAttributeValue("href", null);
                    if (!string.IsNullOrWhiteSpace(href))
                        return href;
                }
            }

            return String.Empty;
        }

        #endregion
    }

    public interface IPlaywrightAutomation
    {
        Task Wait(int milliseconds);

        Task PressKey(string key);

        Task Fill(string locator, string value, IEnumerable<ILocator>? searchInLocators = null);

        Task<bool> Navigate(string url);

        Task<IReadOnlyList<ILocator>> Loop(string locators);

        Task<IReadOnlyList<ILocator>> Loop(string locators, string keywords);

        Task<KeyValuePair<string, IPage?>?> ClickOpenTab(string? locator = null, IEnumerable<ILocator>? clickThisLocators = null);

        Task<bool> CloseNewTab(string? locator = null);

        Task<bool> Click(string? locator = null, IEnumerable<ILocator>? clickThisLocators = null);

        Task Click(string locator, int index);

        Task<ILocator?> Find(string locators, string keywords, IEnumerable<ILocator>? searchInLocators = null);

        Task<IReadOnlyList<ILocator>> Filter(string locators, string keywords, string condition, ILocator? searchInLocators = null);

        Task<string> Observe();

        Task<List<string>> GetHtmlList(ILocator locator);

        Task<List<string>> GetOuterHtmlList(ILocator locator);
    }
}
