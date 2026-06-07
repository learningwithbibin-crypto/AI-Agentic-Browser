using Microsoft.Playwright;
using System;
using System.Collections.Generic;

namespace AgenticBrowserAI.Models
{
    public class AIGoalContext
    {
        public AIGoalContext(int id)
        {
            ContextId = id;
        }

        public int ContextId { get; set; }
        public UserPromptInput UserPromptInput { get; set; }
        public int RestartStepId { get; set; }
        public IPage BrowserTab { get; set; }
        protected IBrowserContext BrowserContext => BrowserTab.Context;
        public PlannerContext PlannerContext { get; set; }
        public ExecutorContext ExecutorContext { get; set; }

        // IMPROVE: move to a class
        public string? ActiveTabId { get; set; }
        public IPage? ActiveTab { get; set; }
        public Dictionary<string, IPage> BrowserTabs { get; } = new();

        public void SetActivePage(string key)
        {
            if (!BrowserTabs.ContainsKey(key))
                throw new Exception($"Page '{key}' not found");

            ActiveTab = BrowserTabs[key];
            ActiveTabId = key;
        }
    }

    public enum AITaskStatus
    {
        UnInitialised,
        Pending,
        Processing,
        Failed,
        Completed,
        Cancelled,
        Retrying,
        Restarting,
        Planned
    }

    public class GoalProgress
    {
        public int GoalId { get; set; }
        public int StepId { get; set; }
        public string StepKey { get; set; }
        public string Goal { get; set; }
        public string StepObjective { get; set; }
        public AITaskStatus Status { get; set; }
    }

    public class AnalysisResult
    {
        public bool NeedsReplan { get; set; }
        public bool IsLoopStep { get; set; }
        public bool ExecuteSubSteps { get; set; }
        public UserPromptInput ReplanPrompt { get; set; }
    }

    public class TaskItem
    {
        public string Id { get; set; }
        public string Goal { get; set; }
        public string Status { get; set; }
    }

    public class StepLog
    {
        public string TaskId { get; set; }
        public string ActionType { get; set; }
        public string Status { get; set; }
        public string Observation { get; set; }
    }

    public class UIElement
    {
        public string Role { get; set; }
        public string Name { get; set; }
    }

    public class AgentState
    {
        public string Goal { get; set; }
        public List<StepLog> History { get; set; } = new();
        public List<UIElement> CurrentUI { get; set; } = new();
        public Dictionary<string, object> Memory;
    }
}
