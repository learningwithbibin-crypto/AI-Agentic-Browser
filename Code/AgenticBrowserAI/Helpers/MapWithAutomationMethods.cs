using AgenticBrowserAI.Models;
using Microsoft.Playwright;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AgenticBrowserAI.Helpers
{
    public class MapWithAutomationMethods
    {
        Dictionary<string, Action<string[]>> actions;
        IPlaywrightAutomation automation;
        internal ResultWrapper Result;

        public MapWithAutomationMethods(IPlaywrightAutomation automation)
        {
            this.automation = automation;
        }

        public async Task Run(StepDetails step)
        {
            if (String.IsNullOrWhiteSpace(step.FunctionName) && step.FunctionParameters == null && step.Input?.Locators == null)
            {
                return;
            }

            string command = step.FunctionName;
            string[] args = step.FunctionParameters;

            string firstParameter = null;
            string secondParameter = null;

            if (args?.Length > 0)
                firstParameter = args[0];

            if (args?.Length > 1)
                secondParameter = args[1];

            IEnumerable<ILocator>? locatorParameter = null;

            if (step.Input != null && step.Input.Type == ResultType.Locators)
            {
                locatorParameter = step.Input.Locators;
            }

            Result = new ResultWrapper();

            switch (command)
            {
                case "wait":
                    await automation.Wait(int.Parse(firstParameter));
                    Result.Type = ResultType.Nothing;
                    break;

                case "loop":
                    IEnumerable<ILocator> loopLocators = await automation.Loop(firstParameter, secondParameter);
                    Result.Type = ResultType.Locators;
                    Result.Locators = await FilterExistingAsync(loopLocators);
                    break;

                case "click_open_in_new_tab":
                    var tabRef = await automation.ClickOpenTab(firstParameter, locatorParameter);
                    Result.NewTab = tabRef;
                    Result.Type = ResultType.NewTab;
                    break;
                case "close_new_tab":
                    Result.BooleanValue = await automation.CloseNewTab(firstParameter);
                    Result.Type = ResultType.Boolean;
                    break;
                case "click":
                    Result.BooleanValue = await automation.Click(firstParameter, locatorParameter);
                    Result.Type = ResultType.Boolean;
                    break;

                case "fill":
                    await automation.Fill(firstParameter, secondParameter, locatorParameter);
                    Result.Type = ResultType.Nothing;
                    break;

                case "press":
                    await automation.PressKey(firstParameter);
                    Result.Type = ResultType.Nothing;
                    break;

                case "find":
                    ILocator? findLocator = await automation.Find(firstParameter, secondParameter, locatorParameter);
                    Result.Type = ResultType.Locators;
                    Result.Locators = findLocator == null ? null : new List<ILocator> { findLocator };
                    break;

                case "filter":
                    IEnumerable<ILocator> locators = await automation.Filter(firstParameter, secondParameter, args[2]);
                    Result.Type = ResultType.Locators;
                    Result.Locators = await FilterExistingAsync(locators);
                    break;

                case "observe":
                    Result.HTML = await automation.Observe();
                    Result.Type = ResultType.CallLLM;
                    break;

                case "navigate":
                    string url = firstParameter;
                    Result.BooleanValue = await automation.Navigate(url);
                    Result.Type = ResultType.Boolean;
                    break;

                case "user_action_required":
                    await automation.Wait(2000);
                    Result.Type = ResultType.Nothing;
                    break;
            }
        }

        private static async Task<List<ILocator>> FilterExistingAsync(IEnumerable<ILocator> locators)
        {
            var tasks = locators.Select(async l => new
            {
                Locator = l,
                Exists = await l.CountAsync() > 0
            });

            var results = await Task.WhenAll(tasks);

            return results
                .Where(x => x.Exists)
                .Select(x => x.Locator)
                .ToList();
        }
    }
}
