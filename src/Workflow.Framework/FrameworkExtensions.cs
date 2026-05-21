using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using Workflow.Abstractions;
using Workflow.Engine;
using Workflow.Persistence.EFCore;

namespace Workflow.Framework;

/// <summary>
/// Options for one-line workflow framework setup.
/// </summary>
public sealed class WorkflowFrameworkOptions
{
    /// <summary>
    /// SQL Server connection string used by workflow persistence.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Dashboard route path.
    /// </summary>
    public string DashboardPath { get; set; } = "/workflow-dashboard";

    /// <summary>
    /// Enables automatic database migration on startup.
    /// </summary>
    public bool AutoMigrateDatabase { get; set; } = true;

    /// <summary>
    /// Enables simple Basic authentication for dashboard pages and endpoints.
    /// </summary>
    public bool DashboardAuthEnabled { get; set; }

    /// <summary>
    /// Dashboard Basic auth username.
    /// </summary>
    public string DashboardUsername { get; set; } = "admin";

    /// <summary>
    /// Dashboard Basic auth password.
    /// </summary>
    public string DashboardPassword { get; set; } = "admin";
}

/// <summary>
/// Framework extension methods for service and dashboard wiring.
/// </summary>
public static class FrameworkExtensions
{
    /// <summary>
    /// Registers workflow engine, SQL persistence, and required adapters.
    /// </summary>
    public static IServiceCollection AddWorkflowFramework(
        this IServiceCollection services,
        Action<WorkflowFrameworkOptions> configure)
    {
        var options = new WorkflowFrameworkOptions();
        configure(options);

        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            throw new InvalidOperationException("Workflow framework connection string is required.");
        }

        services.AddSingleton(options);
        services.AddWorkflowPersistenceSqlServer(options.ConnectionString);
        services.AddWorkflowEngine();
        services.AddSingleton<IGroupProvider>(new StaticGroupProvider(new Dictionary<string, List<string>>()));
        return services;
    }

    /// <summary>
    /// Applies pending migrations (optional) and maps built-in dashboard endpoints.
    /// </summary>
    public static WebApplication UseWorkflowFrameworkDashboard(this WebApplication app)
    {
        var options = app.Services.GetRequiredService<WorkflowFrameworkOptions>();
        if (options.AutoMigrateDatabase)
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<WorkflowDbContext>();
            db.Database.Migrate();
        }

        var path = options.DashboardPath.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(path))
        {
            path = "/workflow-dashboard";
        }

        if (options.DashboardAuthEnabled)
        {
            app.Use(async (context, next) =>
            {
                if (!context.Request.Path.StartsWithSegments(path, StringComparison.OrdinalIgnoreCase))
                {
                    await next();
                    return;
                }

                if (!TryValidateBasicAuth(context.Request.Headers.Authorization, options))
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    context.Response.Headers.Append("WWW-Authenticate", @"Basic realm=""Workflow Dashboard""");
                    await context.Response.WriteAsync("Unauthorized");
                    return;
                }

                await next();
            });
        }

        app.MapGet(path, static () =>
        {
            return Results.Content(GetDashboardHtml(), "text/html", Encoding.UTF8);
        });

        app.MapGet($"{path}/api/definitions", async (IProcessDefinitionRepository repo, string? search, CancellationToken ct) =>
        {
            var all = await repo.GetAllAsync(ct);
            var payload = all
                .OrderBy(x => x.ProcessKey)
                .ThenByDescending(x => x.Version)
                .Where(x =>
                    string.IsNullOrWhiteSpace(search)
                    || x.ProcessKey.Contains(search, StringComparison.OrdinalIgnoreCase)
                    || x.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
                .Select(x => new
                {
                    x.Id,
                    x.ProcessKey,
                    x.Version,
                    x.Name,
                    Status = x.Status.ToString(),
                    NodeCount = x.Nodes.Count
                });
            return Results.Ok(payload);
        });

        app.MapGet($"{path}/api/instances", async (IProcessInstanceRepository repo, IProcessDefinitionRepository definitionRepo, string? state, string? search, CancellationToken ct) =>
        {
            var instances = await repo.GetRecentAsync(500, ct);
            var definitions = await definitionRepo.GetAllAsync(ct);
            var definitionMap = definitions.ToDictionary(x => x.Id, x => x);
            var query = instances.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(state))
            {
                query = query.Where(x => string.Equals(x.State, state, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(x =>
                    x.ProcessKey.Contains(search, StringComparison.OrdinalIgnoreCase)
                    || x.RequestId.Contains(search, StringComparison.OrdinalIgnoreCase));
            }

            var payload = query.Select(x => ToDashboardInstance(x, definitionMap));
            return Results.Ok(payload);
        });

        app.MapGet($"{path}/api/failed", async (IProcessInstanceRepository repo, CancellationToken ct) =>
        {
            var instances = await repo.GetRecentAsync(500, ct);
            var failed = instances
                .Where(x =>
                    string.Equals(x.State, "Rejected", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(x.State, "ReworkRequested", StringComparison.OrdinalIgnoreCase))
                .Select(ToDashboardInstance);
            return Results.Ok(failed);
        });

        app.MapGet($"{path}/api/stats", async (IProcessDefinitionRepository defRepo, IProcessInstanceRepository instanceRepo, CancellationToken ct) =>
        {
            var definitions = await defRepo.GetAllAsync(ct);
            var instances = await instanceRepo.GetRecentAsync(1000, ct);
            var stats = new
            {
                DefinitionCount = definitions.Count,
                PublishedCount = definitions.Count(x => x.Status == ProcessDefinitionStatus.Published),
                DraftCount = definitions.Count(x => x.Status == ProcessDefinitionStatus.Draft),
                InstanceCount = instances.Count,
                ApprovedInstances = instances.Count(x => string.Equals(x.State, "Approved", StringComparison.OrdinalIgnoreCase)),
                RejectedInstances = instances.Count(x => string.Equals(x.State, "Rejected", StringComparison.OrdinalIgnoreCase))
            };
            return Results.Ok(stats);
        });

        app.MapPost($"{path}/api/steps/{{stepId:guid}}/{{actionName}}", async (
            Guid stepId,
            string actionName,
            DashboardStepActionRequest request,
            IWorkflowEngine engine,
            CancellationToken ct) =>
        {
            if (!TryMapStepAction(actionName, out var action))
            {
                return Results.BadRequest("Unsupported action.");
            }

            var result = await engine.ExecuteStepActionAsync(new StepActionRequest
            {
                StepId = stepId,
                Action = action,
                UserId = request.UserId,
                Comment = request.Comment
            }, ct);
            return Results.Ok(result);
        });

        app.MapPost($"{path}/api/retry/{{instanceId:guid}}", async (
            Guid instanceId,
            RetryRequest request,
            IProcessInstanceRepository instanceRepo,
            IUnitOfWork unitOfWork,
            CancellationToken ct) =>
        {
            var instance = await instanceRepo.GetByIdAsync(instanceId, ct);
            if (instance is null)
            {
                return Results.NotFound();
            }

            if (!string.Equals(instance.State, "Rejected", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(instance.State, "ReworkRequested", StringComparison.OrdinalIgnoreCase))
            {
                return Results.Conflict("Only rejected/rework instances can be retried.");
            }

            foreach (var step in instance.Steps.Where(x => x.Status is StepStatus.Rejected or StepStatus.ReworkRequested))
            {
                step.Status = StepStatus.Pending;
                step.CustomStatusKey = "pending";
                step.CustomStatusName = "Pending";
                step.History.Add(new StepStatusHistoryItem
                {
                    ChangedBy = request.UserId,
                    NewStatus = StepStatus.Pending,
                    CustomStatusKey = "pending",
                    CustomStatusName = "Pending",
                    Comment = request.Comment ?? "Retry from dashboard"
                });
            }

            instance.State = "InProgress";
            await instanceRepo.UpsertAsync(instance, ct);
            await unitOfWork.CommitAsync(ct);
            return Results.Ok(ToDashboardInstance(instance));
        });

        return app;
    }

    private static bool TryValidateBasicAuth(StringValues authorizationHeader, WorkflowFrameworkOptions options)
    {
        var header = authorizationHeader.ToString();
        if (string.IsNullOrWhiteSpace(header) || !header.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            var encoded = header["Basic ".Length..].Trim();
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
            var idx = decoded.IndexOf(':');
            if (idx <= 0)
            {
                return false;
            }

            var user = decoded[..idx];
            var pass = decoded[(idx + 1)..];
            return string.Equals(user, options.DashboardUsername, StringComparison.Ordinal)
                && string.Equals(pass, options.DashboardPassword, StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    private static bool TryMapStepAction(string actionName, out StepActionType action)
    {
        switch (actionName.ToLowerInvariant())
        {
            case "claim":
                action = StepActionType.Claim;
                return true;
            case "approve":
                action = StepActionType.Approve;
                return true;
            case "reject":
                action = StepActionType.Reject;
                return true;
            case "rework":
                action = StepActionType.Rework;
                return true;
            default:
                action = StepActionType.Claim;
                return false;
        }
    }

    private static object ToDashboardInstance(ProcessInstance x) =>
        ToDashboardInstance(x, new Dictionary<Guid, ProcessDefinition>());

    private static object ToDashboardInstance(ProcessInstance x, IReadOnlyDictionary<Guid, ProcessDefinition> definitions) => new
    {
        x.Id,
        x.ProcessKey,
        x.DefinitionVersion,
        x.RequestId,
        x.State,
        CurrentStage = GetCurrentStageLabel(x, definitions.TryGetValue(x.DefinitionId, out var def) ? def : null),
        StepCount = x.Steps.Count,
        UpdatedAtUtc = x.Steps.Select(s => s.History.LastOrDefault()?.ChangedAtUtc ?? s.CreatedAtUtc).DefaultIfEmpty().Max(),
        Steps = x.Steps.Select(s => new
        {
            s.Id,
            s.Name,
            Status = s.Status.ToString(),
            s.CustomStatusName,
            ResponsibleUsers = s.Assignments.Select(a => a.PrincipalId).ToList()
        })
    };

    private static string GetCurrentStageLabel(ProcessInstance instance, ProcessDefinition? definition)
    {
        var active = instance.Steps
            .Where(step => step.Status is StepStatus.Pending or StepStatus.Unclaimed or StepStatus.InProgress)
            .FirstOrDefault();

        if (active is not null)
        {
            var stage = definition is null ? null : ResolveStageNumber(definition, active.NodeId);
            return stage.HasValue ? $"Этап {stage.Value} — {active.Name}" : active.Name;
        }

        return instance.State switch
        {
            "Approved" => "Процесс завершен",
            "Rejected" => "Отклонено",
            "ReworkRequested" => "Возврат на доработку",
            _ => "Ожидание"
        };
    }

    private static int? ResolveStageNumber(ProcessDefinition definition, Guid nodeId)
    {
        var ordered = definition.Nodes.OrderBy(x => x.SortOrder).ToList();
        for (var i = 0; i < ordered.Count; i++)
        {
            var root = ordered[i];
            if (root.Id == nodeId || ContainsNode(root.Children, nodeId))
            {
                return i + 1;
            }
        }

        return null;
    }

    private static bool ContainsNode(IEnumerable<NodeDefinition> nodes, Guid targetNodeId)
    {
        foreach (var node in nodes)
        {
            if (node.Id == targetNodeId || ContainsNode(node.Children, targetNodeId))
            {
                return true;
            }
        }

        return false;
    }

    private static string GetDashboardHtml() =>
        """
        <!doctype html>
        <html>
        <head>
          <meta charset="utf-8" />
          <title>Workflow Dashboard</title>
          <style>
            body { font-family: Segoe UI, Arial, sans-serif; margin: 0; background:#f5f7fb; color:#1f2a44; }
            .shell { display:grid; grid-template-columns: 230px 1fr; min-height:100vh; }
            .sidebar { background:#1f336f; color:#fff; padding:18px; }
            .sidebar h2 { font-size:18px; margin: 0 0 14px; }
            .nav-link { display:block; color:#d9e6ff; text-decoration:none; padding:8px 10px; border-radius:7px; margin-bottom:6px; }
            .nav-link.active, .nav-link:hover { background:#2f63ff; color:#fff; }
            .main { padding:20px; }
            h1 { margin:0 0 6px; }
            .toolbar { display:flex; gap:8px; margin: 10px 0 14px; flex-wrap:wrap; }
            .toolbar input, .toolbar select { padding:8px; border:1px solid #d6deee; border-radius:7px; }
            .card { background:white; border-radius:10px; padding:16px; margin-bottom:16px; box-shadow:0 1px 4px rgba(0,0,0,0.08); }
            .grid { display:grid; grid-template-columns: repeat(3, minmax(150px, 1fr)); gap:12px; }
            .metric { background:#eef3ff; border-radius:8px; padding:12px; }
            table { width:100%; border-collapse:collapse; background:white; }
            th, td { border-bottom:1px solid #e6e9f2; text-align:left; padding:8px; font-size:13px; vertical-align:top; }
            th { background:#f3f5fb; }
            button { padding:8px 12px; border:none; background:#2f63ff; color:#fff; border-radius:6px; cursor:pointer; }
            .btn-secondary { background:#5f6d89; }
            .btn-danger { background:#be2c2c; }
            .btn-success { background:#237844; }
            .hidden { display:none; }
            .step-chip { background:#eff4ff; border:1px solid #d7e1fb; border-radius:999px; padding:2px 8px; font-size:11px; margin-right:4px; display:inline-block; margin-bottom:4px; }
          </style>
        </head>
        <body>
          <div class="shell">
            <aside class="sidebar">
              <h2>Workflow Dashboard</h2>
              <a href="#" class="nav-link active" data-tab="overview">Overview</a>
              <a href="#" class="nav-link" data-tab="definitions">Definitions</a>
              <a href="#" class="nav-link" data-tab="instances">Instances</a>
              <a href="#" class="nav-link" data-tab="failed">Failed</a>
              <a href="#" class="nav-link" data-tab="retry">Retry</a>
            </aside>
            <main class="main">
              <h1>Operational Monitoring</h1>
              <p>Hangfire-like UI for definitions, runtime, failed jobs, and manual actions.</p>
              <div class="toolbar">
                <input id="searchInput" placeholder="Search process key or request id" />
                <select id="stateFilter">
                  <option value="">All states</option>
                  <option value="InProgress">InProgress</option>
                  <option value="Approved">Approved</option>
                  <option value="Rejected">Rejected</option>
                  <option value="ReworkRequested">ReworkRequested</option>
                </select>
                <button onclick="reload()">Refresh</button>
              </div>

              <section id="tab-overview">
                <div class="card">
                  <h3>Statistics</h3>
                  <div id="stats" class="grid"></div>
                </div>
              </section>

              <section id="tab-definitions" class="hidden">
                <div class="card">
                  <h3>Definitions</h3>
                  <table>
                    <thead><tr><th>ProcessKey</th><th>Version</th><th>Status</th><th>Name</th><th>Nodes</th></tr></thead>
                    <tbody id="definitionsBody"></tbody>
                  </table>
                </div>
              </section>

              <section id="tab-instances" class="hidden">
                <div class="card">
                  <h3>Instances</h3>
                  <table>
                    <thead><tr><th>ProcessKey</th><th>RequestId</th><th>State</th><th>Current stage</th><th>Version</th><th>Steps</th><th>Actions</th><th>Updated (UTC)</th></tr></thead>
                    <tbody id="instancesBody"></tbody>
                  </table>
                </div>
              </section>

              <section id="tab-failed" class="hidden">
                <div class="card">
                  <h3>Failed / Rework</h3>
                  <table>
                    <thead><tr><th>ProcessKey</th><th>RequestId</th><th>State</th><th>Retry</th></tr></thead>
                    <tbody id="failedBody"></tbody>
                  </table>
                </div>
              </section>

              <section id="tab-retry" class="hidden">
                <div class="card">
                  <h3>Retry queue</h3>
                  <p>Use retry buttons from Failed tab to move workflows back to InProgress.</p>
                </div>
              </section>
            </main>
          </div>
          <script>
            async function loadJson(url) {
              const response = await fetch(url);
              if (!response.ok) throw new Error("Request failed: " + url);
              return await response.json();
            }

            async function postJson(url, payload) {
              const response = await fetch(url, {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify(payload || {})
              });
              if (!response.ok) {
                const text = await response.text();
                throw new Error(text || ("Request failed: " + url));
              }
              return await response.json().catch(() => ({}));
            }

            function renderStats(s) {
              const entries = [
                ["Definitions", s.definitionCount],
                ["Published", s.publishedCount],
                ["Drafts", s.draftCount],
                ["Instances", s.instanceCount],
                ["Approved", s.approvedInstances],
                ["Rejected", s.rejectedInstances]
              ];
              document.getElementById("stats").innerHTML = entries.map(([k,v]) =>
                `<div class='metric'><div style='font-size:12px;color:#546284;'>${k}</div><div style='font-size:22px;font-weight:600;'>${v}</div></div>`
              ).join("");
            }

            function renderTable(id, rows, mapFn) {
              document.getElementById(id).innerHTML = rows.map(mapFn).join("");
            }

            function renderStepActions(instance) {
              const first = (instance.steps || []).find(s => s.status === "Pending" || s.status === "Unclaimed" || s.status === "InProgress");
              if (!first) return "-";
              const user = prompt("UserId for step action:", (first.responsibleUsers || [])[0] || "dashboard.user");
              return user;
            }

            async function executeStepAction(stepId, actionName) {
              const userId = prompt("UserId:", "dashboard.user");
              if (!userId) return;
              const comment = prompt("Comment (optional):", "") || "";
              await postJson(window.basePath + `/api/steps/${stepId}/${actionName}`, { userId, comment });
              await reload();
            }

            async function retryInstance(instanceId) {
              const userId = prompt("Retry requested by userId:", "dashboard.user");
              if (!userId) return;
              const comment = prompt("Retry comment:", "Retry from dashboard") || "";
              await postJson(window.basePath + `/api/retry/${instanceId}`, { userId, comment });
              await reload();
            }

            async function reload() {
              const search = encodeURIComponent(document.getElementById("searchInput").value || "");
              const state = encodeURIComponent(document.getElementById("stateFilter").value || "");
              const [stats, definitions, instances, failed] = await Promise.all([
                loadJson(window.basePath + "/api/stats"),
                loadJson(window.basePath + "/api/definitions?search=" + search),
                loadJson(window.basePath + "/api/instances?search=" + search + "&state=" + state),
                loadJson(window.basePath + "/api/failed")
              ]);
              renderStats(stats);
              renderTable("definitionsBody", definitions, x =>
                `<tr><td>${x.processKey}</td><td>${x.version}</td><td>${x.status}</td><td>${x.name}</td><td>${x.nodeCount}</td></tr>`
              );
              renderTable("instancesBody", instances, x =>
                `<tr>
                  <td>${x.processKey}</td>
                  <td>${x.requestId}</td>
                  <td>${x.state}</td>
                  <td>${x.currentStage ?? "-"}</td>
                  <td>${x.definitionVersion}</td>
                  <td>${(x.steps || []).map(s => `<span class="step-chip">${s.name}:${s.customStatusName}</span>`).join("")}</td>
                  <td>${(x.steps || []).map(s =>
                    `<button style="margin:2px" onclick="executeStepAction('${s.id}','claim')">Claim</button>
                     <button class='btn-success' style="margin:2px" onclick="executeStepAction('${s.id}','approve')">Approve</button>
                     <button class='btn-danger' style="margin:2px" onclick="executeStepAction('${s.id}','rework')">Rework</button>`
                  ).join("") || "-"}</td>
                  <td>${x.updatedAtUtc ?? ""}</td>
                </tr>`
              );
              renderTable("failedBody", failed, x =>
                `<tr><td>${x.processKey}</td><td>${x.requestId}</td><td>${x.state}</td><td><button class='btn-secondary' onclick="retryInstance('${x.id}')">Retry</button></td></tr>`
              );
            }

            window.basePath = window.location.pathname.replace(/\/$/, "");
            document.querySelectorAll(".nav-link").forEach(link => {
              link.addEventListener("click", e => {
                e.preventDefault();
                document.querySelectorAll(".nav-link").forEach(x => x.classList.remove("active"));
                link.classList.add("active");
                const tab = link.getAttribute("data-tab");
                document.querySelectorAll("section[id^='tab-']").forEach(s => s.classList.add("hidden"));
                document.getElementById("tab-" + tab).classList.remove("hidden");
              });
            });

            reload().catch(err => alert(err.message));
          </script>
        </body>
        </html>
        """;
}

/// <summary>
/// Dashboard request body for step action operations.
/// </summary>
public sealed class DashboardStepActionRequest
{
    /// <summary>
    /// Actor user identifier.
    /// </summary>
    public string UserId { get; set; } = "dashboard.user";

    /// <summary>
    /// Optional action comment.
    /// </summary>
    public string? Comment { get; set; }
}

/// <summary>
/// Dashboard request body for retry operation.
/// </summary>
public sealed class RetryRequest
{
    /// <summary>
    /// Actor user identifier.
    /// </summary>
    public string UserId { get; set; } = "dashboard.user";

    /// <summary>
    /// Optional retry comment.
    /// </summary>
    public string? Comment { get; set; }
}
