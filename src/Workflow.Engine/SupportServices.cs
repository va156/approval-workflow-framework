using Microsoft.Extensions.DependencyInjection;
using Workflow.Abstractions;

namespace Workflow.Engine;

/// <summary>
/// Business calendar that skips weekend days.
/// </summary>
public sealed class WeekdayBusinessCalendar : IBusinessCalendar
{
    /// <inheritdoc />
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

/// <summary>
/// Minimal fallback evaluator used when expression parser does not handle condition.
/// </summary>
public sealed class FallbackConditionEvaluator : IConditionEvaluator
{
    /// <inheritdoc />
    public bool Evaluate(string expression, IReadOnlyDictionary<string, object?> data) =>
        string.Equals(expression.Trim(), "true", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// No-op notification service for local demos and tests.
/// </summary>
public sealed class NullNotificationService : INotificationService
{
    /// <inheritdoc />
    public Task SendAsync(NotificationMessage message, CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>
/// In-memory group provider used in samples and smoke tests.
/// </summary>
public sealed class StaticGroupProvider : IGroupProvider
{
    private readonly Dictionary<string, List<string>> _groups = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Creates provider with preconfigured group-to-user map.
    /// </summary>
    public StaticGroupProvider(Dictionary<string, List<string>> groups)
    {
        _groups = groups;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> GetGroupMembersAsync(string groupId, CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<string>>(_groups.TryGetValue(groupId, out var users) ? users : []);
    }
}

/// <summary>
/// Dependency injection helpers for engine core services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers core workflow engine services.
    /// </summary>
    public static IServiceCollection AddWorkflowEngine(this IServiceCollection services)
    {
        services.AddSingleton<IBusinessCalendar, WeekdayBusinessCalendar>();
        services.AddSingleton<IConditionEvaluator, FallbackConditionEvaluator>();
        services.AddSingleton<INotificationService, NullNotificationService>();
        services.AddScoped<IWorkflowEngine, WorkflowEngine>();
        return services;
    }
}
