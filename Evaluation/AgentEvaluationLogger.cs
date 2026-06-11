using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentHarness.Evaluation;

public sealed class AgentEvaluationLogger
{
    private readonly ILogger<AgentEvaluationLogger> _logger;

    public AgentEvaluationLogger(ILogger<AgentEvaluationLogger>? logger = null)
    {
        _logger = logger ?? NullLogger<AgentEvaluationLogger>.Instance;
    }

    public void Log(AgentEvaluationRecord record)
    {
        _logger.LogInformation(
            "Agent evaluation {RequestId} succeeded={Succeeded} durationMs={DurationMs} steps={Steps} promptTokens={PromptTokens} completionTokens={CompletionTokens} request={Request} response={Response} error={Error}",
            record.RequestId,
            record.Succeeded,
            record.DurationMs,
            record.Steps,
            record.TotalPromptTokens,
            record.TotalCompletionTokens,
            record.Request,
            record.Response,
            record.Error);
    }
}
