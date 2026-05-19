using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Workflow.Abstractions;

namespace Workflow.Persistence.EFCore;

/// <summary>
/// EF Core database context for workflow definition and runtime storage.
/// </summary>
public sealed class WorkflowDbContext(DbContextOptions<WorkflowDbContext> options) : DbContext(options)
{
    /// <summary>
    /// Process definition versions.
    /// </summary>
    public DbSet<WorkflowDefinitionEntity> Definitions => Set<WorkflowDefinitionEntity>();
    /// <summary>
    /// Runtime process instances.
    /// </summary>
    public DbSet<WorkflowInstanceEntity> Instances => Set<WorkflowInstanceEntity>();
    /// <summary>
    /// Notification templates.
    /// </summary>
    public DbSet<NotificationTemplateEntity> NotificationTemplates => Set<NotificationTemplateEntity>();

    /// <summary>
    /// Configures relational mappings for workflow entities.
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WorkflowDefinitionEntity>(entity =>
        {
            entity.ToTable("WorkflowDefinitions");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.ProcessKey, x.Version }).IsUnique();
            entity.Property(x => x.ProcessKey).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Status).HasConversion<int>();
            entity.Property(x => x.DefinitionJson).HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.Property(x => x.UpdatedAtUtc).IsRequired();
        });

        modelBuilder.Entity<WorkflowInstanceEntity>(entity =>
        {
            entity.ToTable("WorkflowInstances");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.RequestId);
            entity.Property(x => x.ProcessKey).HasMaxLength(200).IsRequired();
            entity.Property(x => x.State).HasMaxLength(100).IsRequired();
            entity.Property(x => x.InstanceJson).HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.Property(x => x.UpdatedAtUtc).IsRequired();
        });

        modelBuilder.Entity<NotificationTemplateEntity>(entity =>
        {
            entity.ToTable("NotificationTemplates");
            entity.HasKey(x => x.Key);
            entity.Property(x => x.Key).HasMaxLength(200);
            entity.Property(x => x.SubjectTemplate).HasColumnType("nvarchar(500)");
            entity.Property(x => x.BodyTemplate).HasColumnType("nvarchar(max)");
        });
    }
}

/// <summary>
/// Persisted process definition version record.
/// </summary>
public sealed class WorkflowDefinitionEntity
{
    /// <summary>Definition identifier.</summary>
    public Guid Id { get; set; }
    /// <summary>Stable process key.</summary>
    public string ProcessKey { get; set; } = string.Empty;
    /// <summary>Definition version.</summary>
    public int Version { get; set; }
    /// <summary>Definition name.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>Definition lifecycle status.</summary>
    public ProcessDefinitionStatus Status { get; set; }
    /// <summary>Serialized definition JSON snapshot.</summary>
    public string DefinitionJson { get; set; } = string.Empty;
    /// <summary>Creation timestamp.</summary>
    public DateTimeOffset CreatedAtUtc { get; set; }
    /// <summary>Last update timestamp.</summary>
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

/// <summary>
/// Persisted runtime process instance record.
/// </summary>
public sealed class WorkflowInstanceEntity
{
    /// <summary>Runtime instance identifier.</summary>
    public Guid Id { get; set; }
    /// <summary>Stable process key.</summary>
    public string ProcessKey { get; set; } = string.Empty;
    /// <summary>Definition identifier.</summary>
    public Guid DefinitionId { get; set; }
    /// <summary>Definition version used at start.</summary>
    public int DefinitionVersion { get; set; }
    /// <summary>Business request identifier.</summary>
    public string RequestId { get; set; } = string.Empty;
    /// <summary>Aggregate process state.</summary>
    public string State { get; set; } = string.Empty;
    /// <summary>Serialized runtime JSON snapshot.</summary>
    public string InstanceJson { get; set; } = string.Empty;
    /// <summary>Creation timestamp.</summary>
    public DateTimeOffset CreatedAtUtc { get; set; }
    /// <summary>Last update timestamp.</summary>
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

/// <summary>
/// Persisted notification template record.
/// </summary>
public sealed class NotificationTemplateEntity
{
    /// <summary>Template key.</summary>
    public string Key { get; set; } = string.Empty;
    /// <summary>Message subject template.</summary>
    public string SubjectTemplate { get; set; } = string.Empty;
    /// <summary>Message body template.</summary>
    public string BodyTemplate { get; set; } = string.Empty;
}

/// <summary>
/// EF implementation of process definition repository.
/// </summary>
public sealed class EfProcessDefinitionRepository(WorkflowDbContext dbContext) : IProcessDefinitionRepository
{
    /// <inheritdoc />
    public async Task<ProcessDefinition?> GetPublishedByKeyAsync(string processKey, CancellationToken cancellationToken)
    {
        var entity = await dbContext.Definitions
            .Where(x => x.ProcessKey == processKey && x.Status == ProcessDefinitionStatus.Published)
            .OrderByDescending(x => x.Version)
            .FirstOrDefaultAsync(cancellationToken);

        return entity is null ? null : JsonSerializer.Deserialize<ProcessDefinition>(entity.DefinitionJson);
    }

    /// <inheritdoc />
    public async Task<ProcessDefinition?> GetByIdAsync(Guid definitionId, CancellationToken cancellationToken)
    {
        var entity = await dbContext.Definitions.FirstOrDefaultAsync(x => x.Id == definitionId, cancellationToken);
        return entity is null ? null : JsonSerializer.Deserialize<ProcessDefinition>(entity.DefinitionJson);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProcessDefinition>> GetByKeyAsync(string processKey, CancellationToken cancellationToken)
    {
        var entities = await dbContext.Definitions
            .Where(x => x.ProcessKey == processKey)
            .OrderByDescending(x => x.Version)
            .ToListAsync(cancellationToken);

        return entities
            .Select(x => JsonSerializer.Deserialize<ProcessDefinition>(x.DefinitionJson))
            .Where(x => x is not null)
            .Cast<ProcessDefinition>()
            .ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProcessDefinition>> GetAllAsync(CancellationToken cancellationToken)
    {
        var entities = await dbContext.Definitions
            .OrderBy(x => x.ProcessKey)
            .ThenByDescending(x => x.Version)
            .ToListAsync(cancellationToken);

        return entities
            .Select(x => JsonSerializer.Deserialize<ProcessDefinition>(x.DefinitionJson))
            .Where(x => x is not null)
            .Cast<ProcessDefinition>()
            .ToList();
    }

    /// <inheritdoc />
    public async Task UpsertAsync(ProcessDefinition definition, CancellationToken cancellationToken)
    {
        var existing = await dbContext.Definitions
            .FirstOrDefaultAsync(x => x.Id == definition.Id, cancellationToken);

        if (existing is null)
        {
            dbContext.Definitions.Add(new WorkflowDefinitionEntity
            {
                Id = definition.Id,
                ProcessKey = definition.ProcessKey,
                Version = definition.Version,
                Name = definition.Name,
                Status = definition.Status,
                DefinitionJson = JsonSerializer.Serialize(definition),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            });
            return;
        }

        existing.Name = definition.Name;
        existing.Status = definition.Status;
        existing.DefinitionJson = JsonSerializer.Serialize(definition);
        existing.UpdatedAtUtc = DateTimeOffset.UtcNow;
    }
}

/// <summary>
/// EF implementation of process instance repository.
/// </summary>
public sealed class EfProcessInstanceRepository(WorkflowDbContext dbContext) : IProcessInstanceRepository
{
    /// <inheritdoc />
    public async Task<ProcessInstance?> GetByIdAsync(Guid instanceId, CancellationToken cancellationToken)
    {
        var entity = await dbContext.Instances.FirstOrDefaultAsync(x => x.Id == instanceId, cancellationToken);
        return entity is null ? null : JsonSerializer.Deserialize<ProcessInstance>(entity.InstanceJson);
    }

    /// <inheritdoc />
    public async Task<ProcessInstance?> GetByStepIdAsync(Guid stepId, CancellationToken cancellationToken)
    {
        // JSON scan is acceptable for MVP and can be replaced with normalized runtime tables later.
        var candidates = await dbContext.Instances.ToListAsync(cancellationToken);
        foreach (var candidate in candidates)
        {
            var instance = JsonSerializer.Deserialize<ProcessInstance>(candidate.InstanceJson);
            if (instance?.Steps.Any(s => s.Id == stepId) == true)
            {
                return instance;
            }
        }

        return null;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProcessInstance>> GetRecentAsync(int take, CancellationToken cancellationToken)
    {
        var safeTake = Math.Max(take, 1);
        var entities = await dbContext.Instances
            .OrderByDescending(x => x.UpdatedAtUtc)
            .Take(safeTake)
            .ToListAsync(cancellationToken);

        return entities
            .Select(x => JsonSerializer.Deserialize<ProcessInstance>(x.InstanceJson))
            .Where(x => x is not null)
            .Cast<ProcessInstance>()
            .ToList();
    }

    /// <inheritdoc />
    public async Task UpsertAsync(ProcessInstance instance, CancellationToken cancellationToken)
    {
        var existing = await dbContext.Instances
            .FirstOrDefaultAsync(x => x.Id == instance.Id, cancellationToken);

        if (existing is null)
        {
            dbContext.Instances.Add(new WorkflowInstanceEntity
            {
                Id = instance.Id,
                ProcessKey = instance.ProcessKey,
                DefinitionId = instance.DefinitionId,
                DefinitionVersion = instance.DefinitionVersion,
                RequestId = instance.RequestId,
                State = instance.State,
                InstanceJson = JsonSerializer.Serialize(instance),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            });
            return;
        }

        existing.State = instance.State;
        existing.InstanceJson = JsonSerializer.Serialize(instance);
        existing.UpdatedAtUtc = DateTimeOffset.UtcNow;
    }
}

/// <summary>
/// EF implementation of notification template repository.
/// </summary>
public sealed class EfNotificationTemplateRepository(WorkflowDbContext dbContext) : INotificationTemplateRepository
{
    /// <inheritdoc />
    public async Task<NotificationTemplate?> GetByKeyAsync(string key, CancellationToken cancellationToken)
    {
        var entity = await dbContext.NotificationTemplates.FirstOrDefaultAsync(x => x.Key == key, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        return new NotificationTemplate
        {
            Key = entity.Key,
            SubjectTemplate = entity.SubjectTemplate,
            BodyTemplate = entity.BodyTemplate
        };
    }
}

/// <summary>
/// EF-backed unit of work implementation.
/// </summary>
public sealed class EfUnitOfWork(WorkflowDbContext dbContext) : IUnitOfWork
{
    /// <inheritdoc />
    public Task CommitAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}

/// <summary>
/// Dependency injection extensions for SQL Server persistence adapter.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers EF Core SQL Server persistence and repositories.
    /// </summary>
    public static IServiceCollection AddWorkflowPersistenceSqlServer(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<WorkflowDbContext>(options => options.UseSqlServer(connectionString));
        services.AddScoped<IProcessDefinitionRepository, EfProcessDefinitionRepository>();
        services.AddScoped<IProcessInstanceRepository, EfProcessInstanceRepository>();
        services.AddScoped<INotificationTemplateRepository, EfNotificationTemplateRepository>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        return services;
    }
}
