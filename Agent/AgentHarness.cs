using System.Threading.Channels;
using AgentHarness.Messaging;

namespace AgentHarness.Agent;

public sealed class AgentHarness
{
    private readonly AgentLoop _agentLoop;
    private readonly ChannelReader<AgentRequest> _reader;

    public AgentHarness(AgentLoop agentLoop, ChannelReader<AgentRequest> reader)
    {
        _agentLoop = agentLoop;
        _reader = reader;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        await foreach (var request in _reader.ReadAllAsync(cancellationToken))
        {
            try
            {
                var response = await _agentLoop.RunAsync(request.UserMessage, cancellationToken);
                request.Completion.SetResult(response);
            }
            catch (Exception exception)
            {
                request.Completion.SetException(exception);
            }
        }
    }
}
