using Microsoft.Extensions.DependencyInjection;
using Workflow.Abstractions;

namespace Workflow.Engine;

public sealed class WeekdayBusinessCalendar : IBusinessCalendar
{
    public DateTimeOffset AddWorkingDays(DateTimeOffset startUtc, int days)
    {
        var current = startUtc;
        var remaining = days;
        while (remaining > 0)
        {
            current = current.AddDays(1);
            if (current.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            {
                continue;
            }

            remaining--;
        }

        return current;
    }
}

public sealed class FallbackConditionEvaluator : IConditionEvaluator
{
    public bool Evaluate(string expression, IReadOnlyDictionary<string, object?> data) =>
        string.Equals(expression.Trim(), "true", StringComparison.OrdinalIgnoreCase);
}

public sealed class NullNotificationService : INotificationService
{
    public Task SendAsync(NotificationMessage message, CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed class StaticGroupProvider : IGroupProvider
{
    private readonly Dictionary<string, List<string>> _groups = new(StringComparer.OrdinalIgnoreCase);

    public StaticGroupProvider(Dictionary<string, List<string>> groups)
    {
        _groups = groups;
    }

    public Task<IReadOnlyList<string>> GetGroupMembersAsync(string groupId, CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<string>>(_groups.TryGetValue(groupId, out var users) ? users : []);
    }
}

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddWorkflowEngine(this IServiceCollection services)
    {
        services.AddSingleton<IBusinessCalendar, WeekdayBusinessCalendar>();
        services.AddSingleton<IConditionEvaluator, FallbackConditionEvaluator>();
        services.AddSingleton<INotificationService, NullNotificationService>();
        services.AddScoped<IWorkflowEngine, WorkflowEngine>();
        return services;
    }
}
