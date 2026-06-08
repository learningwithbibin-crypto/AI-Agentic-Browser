# AI-Agentic-Browser
Star AI Agentic Browser
# Theme: Agentic Web
## Team Member: Mr. Bibin Philip Sam (Software Consultant - .NET Framework)

## Problem Statement
Users spend significant time performing repetitive web tasks such as searching information, comparing options, filling forms, booking services, and managing workflows across multiple websites. Existing tools require manual effort or rigid automation that struggles with changing web interfaces.

## Solution Overview
An AI-powered autonomous web agent that understands user goals, plans tasks, browses websites, extracts information, and completes multi-step actions on the user's behalf. The agent can adapt to website changes, recover from errors, and securely execute tasks with minimal user intervention.

The flow begins when the UI sends the user query to the AgentOrchestrator. The orchestrator delegates the initial planning responsibility to the PlannerAgent, which returns a structured plan. This plan is then shared back with the UI for visibility.

Next, the orchestrator initializes a browser session by calling the PlaywrightRepository, which provides an IPage instance with a persisted authentication context. This ensures that all subsequent actions operate within a consistent browser state.
The core of the system operates inside a loop that processes each step of the plan:

Before executing a step, the AnalyzerAgent performs a pre-analysis, evaluating whether the current step is valid, needs adjustment, or requires replanning.

The ExecutorAgent then performs the actual step execution, interacting with the browser through the BrowserObserver, which provides DOM state or results.

After execution, the AnalyzerAgent performs a post-analysis to assess the outcome and determine next actions.
At both pre- and post-analysis stages, the AnalyzerAgent makes two independent decisions:
Whether a new plan is required (replanning).
Whether sub-steps are needed to complete the current step.

These decisions are communicated back to the AgentOrchestrator, which handles them as follows:

If a new plan is required, the orchestrator calls the PlannerAgent to generate a subplan based on the current context. This new plan is independent and continues execution using the same browser tab.
If sub-steps are required, the orchestrator executes them within the goal context and browser session. These sub-steps are meant to assist in completing the current step.

Throughout execution, the UI is continuously updated with step statuses such as pending, processing, success, or failure. Once all steps are completed, the orchestrator signals that the overall goal has been finished.

## AI Integration
The PlannerAgent uses Azure OpenAI Service to convert a user’s goal into a structured, step-by-step execution plan. It understands the goal context, breaks it into logical actions, and generates an initial workflow for the system.

The PlannerAgent interacts with Microsoft Semantic Kernel to manage prompts, maintain context, and structure the plan in a reusable format. Semantic Kernel enables the PlannerAgent to treat planning as a composable skill and ensures consistency across executions.

When the AnalyzerAgent signals a need for replanning, the PlannerAgent is invoked again through Semantic Kernel with updated context. It then refines or regenerates the plan dynamically, ensuring the system adapts to changes during execution.

In essence, the PlannerAgent acts as the intelligent planning engine, while Semantic Kernel acts as the orchestration layer that connects planning with execution and analysis across agents.

# Setup 
1. Add LLM Key in Code\StarAIAgentUI\Configs\appsettings.json
2. Build project using Visual Studio, it will download the nuget packages.
3. The UI application is built with WinUI 3. net8.0-windows10.0.19041.0 framework and Windows App SDK required.

Used Codex, ChatGpt AI to solve few issues.
