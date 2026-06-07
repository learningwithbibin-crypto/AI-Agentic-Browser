using AgenticBrowserAI.Helpers;
using AgenticBrowserAI.Models;
using System.Threading.Tasks;

namespace AgenticBrowserAI.Agents
{
    public class ExecutorAgent : IExecutorAgent
    {
        IPlaywrightAutomation automation;

        public async Task Run(AIGoalContext context)
        {
            this.automation = new PlaywrightAutomation(context.ActiveTab ?? context.BrowserTab);
            MapWithAutomationMethods mapWithAutomationMethods = new MapWithAutomationMethods(this.automation);

            var step = context.ExecutorContext.Request.Step;

            await mapWithAutomationMethods.Run(step);

            if (step.FunctionName == "observe" && step.FunctionParameters[0] == "MOCK_HTML")
            {
                mapWithAutomationMethods.Result.IsDataMocked = true;
            }

            if (mapWithAutomationMethods.Result.Type == ResultType.Nothing)
            {
                //// Carry forward prior output so later steps can still use it as input.
                //step.Output = step.Input; //  ISSUE: The next step will use the input and can provide no result.
                //mapWithAutomationMethods.Result = step.Output ?? new ResultWrapper { Type = ResultType.Nothing };
            }
            else
            {
                step.Output = mapWithAutomationMethods.Result;
            }

            ExecutorResponse response = mapWithAutomationMethods.Result.ConvertToExecutorResponse();
            context.ExecutorContext.Response = response;
        }
    }

    public interface IExecutorAgent : IBaseAgent
    {
    }
}
