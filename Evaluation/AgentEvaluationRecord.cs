namespace AgentHarness.Evaluation;

public sealed record AgentEvaluationRecord(
    Guid RequestId,
    DateTimeOffset Timestamp,
    string Request,
    string Response,
    bool Succeeded,
    string? Error,
    int Steps,
    int TotalPromptTokens,
    int TotalCompletionTokens,
    long DurationMs);
