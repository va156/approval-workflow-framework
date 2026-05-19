using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Workflow.Abstractions;

namespace Workflow.Persistence.EFCore;

public sealed class WorkflowDbContext(DbContextOptions<WorkflowDbContext> options) : DbContext(options)
{
    public DbSet<WorkflowDefinitionEntity> Definitions => Set<WorkflowDefinitionEntity>();
    public DbSet<WorkflowInstanceEntity> Instances => Set<WorkflowInstanceEntity>();
    public DbSet<NotificationTemplateEntity> NotificationTemplates => Set<NotificationTemplateEntity>();

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

public sealed class WorkflowDefinitionEntity
{
    public Guid Id { get; set; }
    public string ProcessKey { get; set; } = string.Empty;
    public int Version { get; set; }
    public string Name { get; set; } = string.Empty;
    public ProcessDefinitionStatus Status { get; set; }
    public string DefinitionJson { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class WorkflowInstanceEntity
{
    public Guid Id { get; set; }
    public string ProcessKey { get; set; } = string.Empty;
    public Guid DefinitionId { get; set; }
    public int DefinitionVersion { get; set; }
    public string RequestId { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string InstanceJson { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class NotificationTemplateEntity
{
    public string Key { get; set; } = string.Empty;
    public string SubjectTemplate { get; set; } = string.Empty;
    public string BodyTemplate { get; set; } = string.Empty;
}

public sealed class EfProcessDefinitionRepository(WorkflowDbContext dbContext) : IProcessDefinitionRepository
{
    public async Task<ProcessDefinition?> GetPublishedByKeyAsync(string processKey, CancellationToken cancellationToken)
    {
        var entity = await dbContext.Definitions
            .Where(x => x.ProcessKey == processKey && x.Status == ProcessDefinitionStatus.Published)
            .OrderByDescending(x => x.Version)
            .FirstOrDefaultAsync(cancellationToken);

        return entity is null ? null : JsonSerializer.Deserialize<ProcessDefinition>(entity.DefinitionJson);
    }

    public async Task<ProcessDefinition?> GetByIdAsync(Guid definitionId, CancellationToken cancellationToken)
    {
        var entity = await dbContext.Definitions.FirstOrDefaultAsync(x => x.Id == definitionId, cancellationToken);
        return entity is null ? null : JsonSerializer.Deserialize<ProcessDefinition>(entity.DefinitionJson);
    }

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

public sealed class EfProcessInstanceRepository(WorkflowDbContext dbContext) : IProcessInstanceRepository
{
    public async Task<ProcessInstance?> GetByIdAsync(Guid instanceId, CancellationToken cancellationToken)
    {
        var entity = await dbContext.Instances.FirstOrDefaultAsync(x => x.Id == instanceId, cancellationToken);
        return entity is null ? null : JsonSerializer.Deserialize<ProcessInstance>(entity.InstanceJson);
    }

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

public sealed class EfNotificationTemplateRepository(WorkflowDbContext dbContext) : INotificationTemplateRepository
{
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

public sealed class EfUnitOfWork(WorkflowDbContext dbContext) : IUnitOfWork
{
    public Task CommitAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}

public static class ServiceCollectionExtensions
{
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
