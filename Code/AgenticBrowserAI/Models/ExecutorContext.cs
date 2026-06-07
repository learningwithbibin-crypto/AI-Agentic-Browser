using Microsoft.Playwright;
using System.Collections.Generic;

namespace AgenticBrowserAI.Models
{
    public class ExecutorContext
    {
        public ExecutorRequest Request { get; set; }
        public ExecutorResponse Response { get; set; }
    }

    public class ExecutorRequest
    {
        public StepDetails Step { get; set; }
    }

    public class ExecutorResponse : ResultWrapper
    {
    }

    public static class ExecutorResponseExtension
    {
        public static ResultWrapper ConvertToResultWrapper(this ExecutorResponse result)
        {
            ResultWrapper response = new ResultWrapper();
            response.Type = result.Type;
            response.Locators = result.Locators;
            response.StringsValue = result.StringsValue;
            response.HTML = result.HTML;
            response.IsDataMocked = result.IsDataMocked;

            return response;
        }
    }

    public class ResultWrapper
    {
        public string? HTML { get; set; }
        public bool? BooleanValue { get; set; }
        public KeyValuePair<string, IPage?>? NewTab { get; set; }
        public IEnumerable<string>? StringsValue { get; set; }
        public IEnumerable<ILocator>? Locators { get; set; }
        public ResultType Type { get; set; } = ResultType.Nothing;
        public bool IsDataMocked { get; set; }
    }

    public static class ResultWrapperExtension
    {
        public static ExecutorResponse ConvertToExecutorResponse(this ResultWrapper result)
        {
            ExecutorResponse response = new ExecutorResponse()
            {
                Type = result.Type,
                Locators = result.Locators,
                StringsValue = result.StringsValue,
                HTML = result.HTML,
                IsDataMocked = result.IsDataMocked,
                NewTab = result.NewTab
            };

            return response;
        }
    }

    public enum ResultType { Nothing, Boolean, HTML, Strings, Locators, CallLLM, NewTab }
}