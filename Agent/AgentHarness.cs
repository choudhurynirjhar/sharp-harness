using System.Diagnostics;
using System.Threading.Channels;
using AgentHarness.Evaluation;
using AgentHarness.Messaging;

namespace AgentHarness.Agent;

public sealed class AgentHarness
{
    private readonly AgentLoop _agentLoop;
    private readonly ChannelReader<AgentRequest> _reader;
    private readonly AgentEvaluationLogger _evaluationLogger;

    public AgentHarness(
        AgentLoop agentLoop,
        ChannelReader<AgentRequest> reader,
        AgentEvaluationLogger evaluationLogger)
    {
        _agentLoop = agentLoop;
        _reader = reader;
        _evaluationLogger = evaluationLogger;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        await foreach (var request in _reader.ReadAllAsync(cancellationToken))
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var result = await _agentLoop.RunAsync(request.UserMessage, cancellationToken);
                stopwatch.Stop();

                _evaluationLogger.Log(new AgentEvaluationRecord(
                    request.RequestId,
                    request.CreatedAt,
                    request.UserMessage,
                    result.Response,
                    Succeeded: true,
                    Error: null,
                    result.Steps,
                    result.TotalPromptTokens,
                    result.TotalCompletionTokens,
                    stopwatch.ElapsedMilliseconds));

                request.Completion.SetResult(result.Response);
            }
            catch (Exception exception)
            {
                stopwatch.Stop();

                _evaluationLogger.Log(new AgentEvaluationRecord(
                    request.RequestId,
                    request.CreatedAt,
                    request.UserMessage,
                    Response: string.Empty,
                    Succeeded: false,
                    Error: exception.Message,
                    Steps: 0,
                    TotalPromptTokens: 0,
                    TotalCompletionTokens: 0,
                    stopwatch.ElapsedMilliseconds));

                request.Completion.SetException(exception);
            }
        }
    }
}
