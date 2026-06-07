using System;
using System.Text.Json.Serialization;

namespace AgenticBrowserAI.Models
{
    public class AzureOpenAiConfig
    {
        public string Endpoint { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public string DeploymentName { get; set; } = string.Empty;
        public string ApiVersion { get; set; } = "2024-02-15-preview";
    }

    public class PlannerContext
    {
        public PlannerRequest Request { get; set; }
        public PlannerResponse Response { get; set; }
    }

    public class PlannerRequest
    {
        public string SystemPrompt { get; set; } = string.Empty;
        public string UserPrompt { get; set; } = string.Empty;
    }

    public class PlannerResponse
    {
        public string JsonResponse { get; set; } = string.Empty;
        public Plan Plan { get; set; }
    }

    public class Plan
    {
        [JsonPropertyName("goal")]
        public string? Goal { get; set; }

        [JsonPropertyName("goal_id")]
        public int? GoalId { get; set; }

        [JsonPropertyName("task")]
        public string? Task { get; set; }

        [JsonPropertyName("task_id")]
        public int? TaskId { get; set; }

        [JsonPropertyName("plan")]
        public StepDetails[]? Steps { get; set; }
    }

    public class StepDetails
    {
        [JsonPropertyName("step")]
        public int StepId { get; set; }

        [JsonPropertyName("sub_plan")]
        public StepDetails[]? SubSteps { get; set; }    // If loop function, then its steps will be filled here.

        [JsonPropertyName("function_name")]
        public string FunctionName { get; set; } = string.Empty;

        [JsonPropertyName("objective")]
        public string Objective { get; set; } = string.Empty;

        [JsonPropertyName("function_parameters")]
        public string[] FunctionParameters { get; set; } = Array.Empty<string>();

        [JsonPropertyName("is_step_skipable")]
        public bool IsStepSkipable { get; set; }

        public ResultWrapper? ScopedInput { get; set; }
        public ResultWrapper? Input { get; set; }
        public ResultWrapper? Output { get; set; } = new ResultWrapper();

        //public StepDetails ParentStep { get; set; }
        public Plan? ParentPlan { get; set; }
        public Plan? SubPlan { get; set; }
        public AITaskStatus Status { get; set; } = AITaskStatus.UnInitialised;
    }
}
