using AgenticBrowserAI.Helpers;
using AgenticBrowserAI.Models;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AgenticBrowserAI.Agents
{
    public class AgentOrchestrator : IAgentOrchestrator
    {
        readonly IPlaywrightRepository playwrightRepository;
        readonly IPlannerAgent plannerAgent;
        readonly IAnalyzerAgent analyzerAgent;
        readonly IExecutorAgent executorAgent;
        AIGoalContext aiGoalContext = new AIGoalContext(1);

        public AgentOrchestrator(
            IPlaywrightRepository playwrightRepository,
            IPlannerAgent plannerAgent,
            IAnalyzerAgent analyzerAgent,
            IExecutorAgent executorAgent)
        {
            this.playwrightRepository = playwrightRepository;
            this.plannerAgent = plannerAgent;
            this.analyzerAgent = analyzerAgent;
            this.executorAgent = executorAgent;
        }

        public async Task<Plan> Run(UserPromptInput userPromptInput)
        {
            return await Run(userPromptInput, null);
        }

        public async Task<Plan> Run(UserPromptInput userPromptInput, IProgress<GoalProgress> progress, CancellationToken token = default)
        {
            return await RunPlanningFlow(aiGoalContext, userPromptInput, progress, token);
        }

        public async Task Restart(int stepId, IProgress<GoalProgress> progress, CancellationToken token)
        {
            //await ExecutePlan(aiGoalContext, aiGoalContext.PlannerContext.Response.Plan, progress, token);
        }

        private async Task<Plan> RunPlanningFlow(AIGoalContext context, UserPromptInput userPromptInput, IProgress<GoalProgress> progress, CancellationToken token)
        {
            var previousQuery = context.UserPromptInput;
            var previousPlannerContext = context.PlannerContext;

            context.UserPromptInput = userPromptInput;
            await plannerAgent.Run(context);

            var plan = context.PlannerContext.Response.Plan;
            ReportPlannedSteps(plan, progress);

            if (context.BrowserTab == null)
            {
                await CreateMainBrowserTab(context, plan);
            }

            await ExecutePlan(context, plan, progress, token);

            context.UserPromptInput = previousQuery;
            context.PlannerContext = previousPlannerContext;

            return plan;
        }

        private async Task ExecutePlan(AIGoalContext context, Plan plan, IProgress<GoalProgress>? progress = null, CancellationToken token = default)
        {
            if (plan == null || plan.Steps == null || !plan.Steps.Any()) return;

            token.ThrowIfCancellationRequested();

            for (int count = 0; count < plan.Steps.Length; count++)
            {
                if (context.RestartStepId > 0)
                {
                    plan.Steps[count].Output = null;
                    count = context.RestartStepId - 1;
                    context.RestartStepId = 0;
                    continue;
                }

                var step = plan.Steps[count];
                step.ParentPlan = plan;
                FileLogger.Log("STEP: " + step.FunctionName + "(" + string.Join(",", step.FunctionParameters) + ")");

                if (count > 0)
                {
                    if (step.Input == null)
                    {
                        // previous step's output will be the input for current step
                        step.Input = plan.Steps[count - 1].Output;
                    }
                }

                context.ExecutorContext = new ExecutorContext
                {
                    Request = new ExecutorRequest { Step = step }
                };

                try
                {
                    await analyzerAgent.Run(context);
                    var analyzePreStep = await analyzerAgent.AnalyzePreStep(plan, step);
                    if (step.IsStepSkipable) { continue; }

                    ReportStepProgress(plan, step, progress);
                    if (analyzePreStep.NeedsReplan)
                    {
                        await CreateReplanning(context, progress, step, analyzePreStep.ReplanPrompt, token);
                    }

                    if (step.Status != AITaskStatus.Completed)
                    {
                        step.Status = AITaskStatus.Processing;
                        ReportStepProgress(plan, step, progress);

                        await executorAgent.Run(context);
                    }

                    await foreach (var analyzePostStep in analyzerAgent.AnalyzePostStep(plan, step))
                    {
                        if (analyzePostStep.NeedsReplan)
                        {
                            await CreateReplanning(context, progress, step, analyzePostStep.ReplanPrompt, token);
                        }
                        else if (analyzePostStep.ExecuteSubSteps)
                        {
                            Plan newPlan = new Plan()
                            {
                                Task = step.Objective,
                                TaskId = step.StepId,
                                Steps = step.SubSteps
                            };
                            await ExecutePlan(context, newPlan, null, token);
                        }

                        ReportStepProgress(plan, step, progress);
                    }
                    ReportStepProgress(plan, step, progress);
                }
                catch (OperationCanceledException)
                {
                    // mark current step as failed/cancelled
                    step.Status = AITaskStatus.Cancelled;
                    ReportStepProgress(plan, step, progress);
                    throw;
                }
                catch
                {
                    step.Status = AITaskStatus.Failed;
                    ReportStepProgress(plan, step, progress);
                    throw;
                }
            }
        }

        // This method calls LLM for replanning the current step. It will create new SubPlan & Steps
        private async Task CreateReplanning(AIGoalContext context, IProgress<GoalProgress> progress, StepDetails step, UserPromptInput userPromptInput, CancellationToken token)
        {
            var subPlanContext = new AIGoalContext(3)
            {
                BrowserTab = context.BrowserTab,
                ActiveTab = context.ActiveTab,
            };

            step.SubPlan = await RunPlanningFlow(subPlanContext, userPromptInput, progress, token);
            step.Status = AITaskStatus.Completed;
            step.Output = step.SubPlan?.Steps?[^1].Output; // when the subplan completes, the last output will become the parent step's output
        }

        private async Task CreateMainBrowserTab(AIGoalContext context, Plan plan)
        {
            var page = await playwrightRepository.CreatePage(plan.GoalId.ToString());
            context.BrowserTab = page;
        }

        private static void ReportPlannedSteps(Plan plan, IProgress<GoalProgress> progress)
        {
            if (progress == null || plan?.Steps == null)
            {
                return;
            }

            foreach (var step in plan.Steps)
            {
                ReportStepProgress(plan, step, progress);
            }
        }

        private static void ReportStepProgress(Plan plan, StepDetails step, IProgress<GoalProgress>? progress)
        {
            if (progress == null || plan == null)
                return;

            progress?.Report(new GoalProgress
            {
                GoalId = plan.GoalId.Value,
                Goal = plan.Goal,
                StepId = step.StepId,
                //StepKey = $"{plan.GoalId}:{step.StepId}",
                StepObjective = step.Objective,
                Status = step.Status
            });
        }
    }

    public interface IAgentOrchestrator
    {
        Task<Plan> Run(UserPromptInput query);
        Task<Plan> Run(UserPromptInput query, IProgress<GoalProgress> progress, CancellationToken token);
        Task Restart(int stepId, IProgress<GoalProgress> progress, CancellationToken token);
    }

    public interface IBaseAgent
    {
        Task Run(AIGoalContext context);
    }
}
