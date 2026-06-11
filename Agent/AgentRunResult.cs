namespace AgentHarness.Agent;

public sealed record AgentRunResult(
    string Response,
    int Steps,
    int TotalPromptTokens,
    int TotalCompletionTokens,
    TimeSpan Duration);
