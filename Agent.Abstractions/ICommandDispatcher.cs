namespace Agent.Abstractions;

public interface ICommandDispatcher
{
    Task DispatchAsync(AgentCommand command, AgentContext context);
}
