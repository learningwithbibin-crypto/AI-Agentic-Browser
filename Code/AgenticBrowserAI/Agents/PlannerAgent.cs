using AgenticBrowserAI.Models;
using System;
using System.ClientModel.Primitives;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AgenticBrowserAI.Helpers;
using System.IO;

namespace AgenticBrowserAI.Agents
{
    public class PlannerAgent : IPlannerAgent
    {
        private readonly HttpClient _httpClient;
        private readonly AzureOpenAiConfig _config;

        public PlannerAgent(HttpClient httpClient, AzureOpenAiConfig config)
        {
            _httpClient = httpClient;
            _config = config;
        }

        public async Task Run(AIGoalContext context)
        {
            BuildPlannerPrompt(context, context.UserPromptInput);

            var requestJson = context.PlannerContext.Request.SystemPrompt + context.PlannerContext.Request.UserPrompt;

            string filePath = Path.Combine(AppContext.BaseDirectory, "Assets/Data", context.UserPromptInput.FileName);
            BasicHelper.WriteFile(filePath, requestJson);

            var responseFileName = context.UserPromptInput.FileName.Replace("request", "response");
            if (requestJson.Contains("Postman"))
            {
                responseFileName = "task1_step2_response.txt";
            }

            var responseJson = BasicHelper.ReadFile(Path.Combine(AppContext.BaseDirectory, "Assets/Data", responseFileName));

            context.PlannerContext.Response = new PlannerResponse
            {
                JsonResponse = responseJson,
                Plan = BasicHelper.JsonToModel<Plan>(responseJson)
            };

            return;


            var request = context.PlannerContext.Request;

            var url =
                $"{_config.Endpoint}openai/deployments/{_config.DeploymentName}/chat/completions?api-version={_config.ApiVersion}";

            var payload = new
            {
                messages = new[]
                {
                new { role = "system", content = request.SystemPrompt },
                new { role = "user", content = request.UserPrompt }
            },
                temperature = 0.7,
                max_tokens = 800
            };

            var json = JsonSerializer.Serialize(payload);

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
            httpRequest.Headers.Add("api-key", _config.ApiKey);
            httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(httpRequest);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Azure OpenAI Error: {response.StatusCode} - {responseContent}");
            }

            using var doc = JsonDocument.Parse(responseContent);

            var result = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            context.PlannerContext.Response = new PlannerResponse
            {
                JsonResponse = result ?? string.Empty,
                Plan = BasicHelper.JsonToModel<Plan>(result)
            };
        }

        private void BuildPlannerPrompt(AIGoalContext context, UserPromptInput userPrompt)
        {
            string instructionType = userPrompt.IsTask ? "TASK" : "USER GOAL";
            string dataRuleText = $$"""DATA section is for your reference to take decision for this {{instructionType}}.""";
            string dataText = String.Empty;
            PlannerRequest request = new PlannerRequest();

            if (!String.IsNullOrWhiteSpace(userPrompt.Data))
            {
                if (!userPrompt.DataType.Equals(DataType.Unknown))
                {
                    string dataTypeText = $$"""ACTUAL_{{userPrompt.DataType}}_DATA""";
                    if (userPrompt.IsMockedData) { dataTypeText = $$"""MOCKED_{{userPrompt.DataType}}_DATA"""; }
                    dataRuleText = $$"""- {{dataTypeText}}, which is under DATA section, is for your reference to take decision for this {{instructionType}}.""";
                    dataText = $$"""DATA: {{userPrompt.Data}}""";
                }
            }

            request.SystemPrompt = $$"""
        You are an automation planner for a browser agent.
        Your job is to convert {{instructionType}} into a STRICT JSON execution plan. 
        The plan is NOT assumed to be fully executable in one pass. It is an adaptive execution sequence.
        Break {{instructionType}} such that it can be executed independently, if possible.
        """;

            request.UserPrompt = $$"""
        IMPORTANT RULES:
        - Output ONLY valid JSON. No explanation.
        - JSON strings MUST use double quotes. If a selector needs quotes inside the string, use single quotes inside the selector, for example "[role='button']".
        - function_parameters MUST always be an array of strings. For example use ["2000"] instead of [2000], and ["4","13","3"] instead of [4,13,3].
        - Do NOT output markdown links. Use raw URLs only, for example "https://outlook.live.com/mail/".
        - Use ONLY the allowed functions.
        - Do NOT assume CSS selectors.
        - Prefer semantic selectors (role,placeholder,text,id,name,label,aria-label,value,etc.).
        - Break tasks into atomic steps.
        - Include retries or alternative strategies if relevant.
        - User security and privacy are of the utmost importance.        
        {{dataRuleText}}
        TOOLS AVAILABLE: Microsoft.Playwright v1.59.0
        AVAILABLE FUNCTIONS:
        - navigate(url)
        - wait(millisecond)
        - find(locator,keywords)
        - fill(locator, value)
        - click(locator)
        - press(key)
        - filter(locator,keywords, condition)
        - loop(locator,keywords,from_step,to_step,max_iterations)
        - repeat(from_step,to_step,skip_step)
        - user_action_required("true")
        - observe(context_request)
        - store(variable, value)       
        Where,
        - locator: Use selectors to find the element from either role,placeholder,text,id,name,label,aria-label, or value. FORMAT: [key='value']
        - keywords: use '||' for multiple keywords 
        - condition can be either 'AND', 'OR'
        - loop is a control-flow function. The orchestrator resolves locator + keywords, then repeats steps from from_step through to_step for each matching item, up to max_iterations.
        - Inside a loop body, use "CURRENT_LOOP_ITEM" as the click locator when the action should target the item currently being iterated.
        OBSERVATION RULE:
        The agent does not have direct access to runtime UI structure, DOM, or page state. Therefore, whenever the next action depends on unknown interface elements, it MUST insert:
        observe(context_request)
        where, context_request could be either ACTUAL_HTML or MOCK_HTML.
        Execution resumes only after observation results are provided.
        {{dataText}}
        OUTPUT FORMAT:
        {
          "{{(userPrompt.IsTask ? "task" : "goal")}}": string,
          "{{(userPrompt.IsTask ? "task" : "goal")}}_id": number,
          "plan": [
            { "step":number, "function_name": string, "objective":string, "function_parameters": [], "is_step_skipable":bool}
          ]
        }
        {{instructionType}}:{{userPrompt.Query}}
        {{userPrompt.AdditionalSections}}
        """;


            context.PlannerContext = new PlannerContext();
            context.PlannerContext.Request = request;
        }
    }

    public interface IPlannerAgent : IBaseAgent
    {
    }
}
