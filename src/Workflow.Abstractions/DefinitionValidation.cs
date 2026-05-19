namespace Workflow.Abstractions;

public static class WorkflowDefinitionValidator
{
    public static IReadOnlyList<string> Validate(ProcessDefinition definition)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(definition.ProcessKey))
        {
            errors.Add("ProcessKey is required.");
        }

        if (definition.Nodes.Count == 0)
        {
            errors.Add("At least one node is required.");
        }

        var statusKeySet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var status in definition.Statuses)
        {
            if (string.IsNullOrWhiteSpace(status.Key))
            {
                errors.Add("Status key cannot be empty.");
                continue;
            }

            if (!statusKeySet.Add(status.Key))
            {
                errors.Add($"Status key '{status.Key}' must be unique.");
            }

            if (string.IsNullOrWhiteSpace(status.DisplayName))
            {
                errors.Add($"Status '{status.Key}' display name is required.");
            }
        }

        ValidateNodes(definition.Nodes, definition.Statuses, errors);
        return errors;
    }

    public static void EnsureDefaultStatuses(ProcessDefinition definition)
    {
        if (definition.Statuses.Count > 0)
        {
            return;
        }

        definition.Statuses =
        [
            new WorkflowStatusDefinition { Key = "pending", DisplayName = "Pending", Semantic = WorkflowStatusSemantic.Pending, IsDefault = true },
            new WorkflowStatusDefinition { Key = "unclaimed", DisplayName = "Unclaimed", Semantic = WorkflowStatusSemantic.Unclaimed, IsDefault = true },
            new WorkflowStatusDefinition { Key = "in-progress", DisplayName = "In Progress", Semantic = WorkflowStatusSemantic.InProgress, IsDefault = true },
            new WorkflowStatusDefinition { Key = "approved", DisplayName = "Approved", Semantic = WorkflowStatusSemantic.Approved, IsDefault = true },
            new WorkflowStatusDefinition { Key = "rejected", DisplayName = "Rejected", Semantic = WorkflowStatusSemantic.Rejected, IsDefault = true },
            new WorkflowStatusDefinition { Key = "rework-requested", DisplayName = "Rework Requested", Semantic = WorkflowStatusSemantic.ReworkRequested, IsDefault = true },
            new WorkflowStatusDefinition { Key = "cancelled", DisplayName = "Cancelled", Semantic = WorkflowStatusSemantic.Cancelled, IsDefault = true },
            new WorkflowStatusDefinition { Key = "expired", DisplayName = "Expired", Semantic = WorkflowStatusSemantic.Expired, IsDefault = true }
        ];
    }

    private static void ValidateNodes(
        IReadOnlyList<NodeDefinition> nodes,
        IReadOnlyList<WorkflowStatusDefinition> statuses,
        List<string> errors)
    {
        var keys = statuses.Select(x => x.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var node in nodes)
        {
            if (node.Type == NodeType.Block && node.StatusBindings.Count > 0)
            {
                errors.Add($"Node '{node.Name}' is a block and cannot own step status bindings.");
            }

            var semantics = new HashSet<WorkflowStatusSemantic>();
            foreach (var binding in node.StatusBindings)
            {
                if (!semantics.Add(binding.Semantic))
                {
                    errors.Add($"Node '{node.Name}' has duplicated semantic binding '{binding.Semantic}'.");
                }

                if (!keys.Contains(binding.StatusKey))
                {
                    errors.Add($"Node '{node.Name}' binds unknown status '{binding.StatusKey}'.");
                }
            }

            ValidateNodes(node.Children, statuses, errors);
        }
    }
}
