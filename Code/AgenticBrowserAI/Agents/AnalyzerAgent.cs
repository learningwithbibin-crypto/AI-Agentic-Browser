using AgenticBrowserAI.Helpers;
using AgenticBrowserAI.Models;
using AIAgentBot.Utilities;
using Azure;
using HtmlAgilityPack;
using Microsoft.Playwright;
using OpenAI.Assistants;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Geolocation;

namespace AgenticBrowserAI.Agents
{
    public class AnalyzerAgent : IAnalyzerAgent
    {
        AIGoalContext context;
        IPlaywrightAutomation playwrightAutomation;

        public Task Run(AIGoalContext context)
        {
            this.context = context;

            return Task.CompletedTask;
        }

        public async Task<AnalysisResult> AnalyzePreStep(Plan plan, StepDetails step)
        {
            step.Status = AITaskStatus.Pending;

            if (context.ActiveTabId != null && context.ActiveTab == null)
            {
                step.IsStepSkipable = true;
                step.Status = AITaskStatus.Completed;

                if (step.FunctionName == "close_new_tab")
                {
                    context.BrowserTabs.Remove(context.ActiveTabId);
                    context.ActiveTabId = null;
                    context.ActiveTab = null;
                }
            }

            if (step.Input?.Type == ResultType.CallLLM)
            {
                var newPlanQuery = await CreateAnalysisResult(step, step.Input);
                newPlanQuery.NeedsReplan = true;

                return newPlanQuery;
            }

            return new AnalysisResult();
        }

        public async IAsyncEnumerable<AnalysisResult> AnalyzePostStep(Plan plan, StepDetails step)
        {
            if (step.Output != null)
            {
                if (step.Output.NewTab != null)
                {
                    // IMPROVE: move to a method and agent should get notified whenever new tab is created                        
                    context.BrowserTabs.Add(step.Output.NewTab.Value.Key, step.Output.NewTab.Value.Value);
                    context.ActiveTabId = step.Output.NewTab.Value.Key;
                    context.SetActivePage(step.Output.NewTab.Value.Key);
                }
            }

            if (step.FunctionName == "close_new_tab" && context.ActiveTabId != null)
            {
                context.BrowserTabs.Remove(context.ActiveTabId);
                context.ActiveTabId = null;
                context.ActiveTab = null;
            }

            if (step.FunctionName == "loop")
            {
                if (step.SubSteps == null || !step.SubSteps.Any())
                {
                    var planQuery = await CreateAnalysisResult(step, step.Output);
                    planQuery.NeedsReplan = true;
                    yield return planQuery;
                    yield break;
                }

                // Loop through all the elements found one-by-one and the sub-steps of loop will run
                IEnumerable<ILocator>? locatorList = step.Output?.Locators;
                if (locatorList != null && locatorList.Any())
                {
                    foreach (var element in locatorList)
                    {
                        step.SubSteps[0].Input = new ResultWrapper
                        {
                            Type = ResultType.Locators,
                            Locators = new List<ILocator> { element }
                        };

                        yield return new AnalysisResult
                        {
                            ExecuteSubSteps = true
                        };
                    }
                }

                step.Status = AITaskStatus.Completed;
                yield break;
            }

            if (IsFurtherAnalysisRequired(step))
            {
                step.Output = step.Output ?? new ResultWrapper { Type = ResultType.Nothing };
                string data = await GetDataForNewPlan(step);
                step.Output.Type = ResultType.CallLLM;
                step.Output.HTML = data;
                var planQuery = await CreateAnalysisResult(step, step.Output);
                planQuery.NeedsReplan = true;
                yield return planQuery;
                yield break;
            }

            step.Status = AITaskStatus.Completed;
            yield return new AnalysisResult();
        }

        private async Task<string> GetDataForNewPlan(StepDetails step)
        {
            if (step.Input != null && step.Input.Locators != null && step.Input.Locators.Any())
            {
                return await step.Input.Locators.First().InnerHTMLAsync();
            }

            this.playwrightAutomation = new PlaywrightAutomation(context.ActiveTab ?? context.BrowserTab);
            return await playwrightAutomation.Observe();
        }

        private async Task<AnalysisResult> CreateAnalysisResult(StepDetails step, ResultWrapper? input)
        {
            return new AnalysisResult
            {
                ReplanPrompt = new UserPromptInput
                {
                    Data = await GetData(input),
                    IsMockedData = step.Output?.IsDataMocked ?? false,
                    Query = step.Objective,
                    DataType = DataType.HTML,
                    IsTask = true,
                    FileName = ("task" + step.ParentPlan?.TaskId + "_step" + step.StepId + "_request.txt")
                }
            };
        }

        private async Task<string> GetData(ResultWrapper input)
        {
            if (input == null) return "";

            StringBuilder result = new StringBuilder();

            switch (input.Type)
            {
                case ResultType.Strings:
                    foreach (var value in input.StringsValue)
                    {
                        result.Append(value);
                    }
                    break;
                case ResultType.Locators:
                    if (input.Locators != null)
                    {
                        foreach (var locator in input.Locators)
                        {
                            if (locator == null) continue;
                            result.Append(await locator.InnerHTMLAsync());
                        }
                    }
                    break;
                case ResultType.HTML:
                    result.Append(input.HTML);
                    break;
                case ResultType.CallLLM:
                    result.Append(input.HTML);
                    break;
            }
            return result.ToString();
            return HtmlDataMasker.MaskUserData(result.ToString());
        }

        private static bool IsFurtherAnalysisRequired(StepDetails step)
        {
            return step.Output?.Type != ResultType.Nothing
            && (step.Output?.Locators == null || !step.Output.Locators.Any())
            && string.IsNullOrWhiteSpace(step.Output?.HTML)
            && step.Output?.BooleanValue == null
            && step.Output?.NewTab == null
            && (step.Output?.StringsValue == null || !step.Output.StringsValue.Any());
        }

    }

    public interface IAnalyzerAgent : IBaseAgent
    {
        Task<AnalysisResult> AnalyzePreStep(Plan plan, StepDetails step);
        IAsyncEnumerable<AnalysisResult> AnalyzePostStep(Plan plan, StepDetails step);
    }
}
