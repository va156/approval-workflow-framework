using Workflow.Framework;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var connectionString = builder.Configuration.GetConnectionString("WorkflowPrimary")
    ?? "Server=(localdb)\\MSSQLLocalDB;Database=WorkflowEngine;Trusted_Connection=True;TrustServerCertificate=True";

builder.Services.AddWorkflowFramework(options =>
{
    options.ConnectionString = connectionString;
    options.DashboardPath = builder.Configuration.GetValue<string>("WorkflowFramework:DashboardPath") ?? "/workflow-dashboard";
    options.AutoMigrateDatabase = builder.Configuration.GetValue<bool?>("WorkflowFramework:AutoMigrateDatabase") ?? true;
    options.DashboardAuthEnabled = builder.Configuration.GetValue<bool?>("WorkflowFramework:DashboardAuthEnabled") ?? false;
    options.DashboardUsername = builder.Configuration.GetValue<string>("WorkflowFramework:DashboardUsername") ?? "admin";
    options.DashboardPassword = builder.Configuration.GetValue<string>("WorkflowFramework:DashboardPassword") ?? "admin";
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.UseWorkflowFrameworkDashboard();
app.Run();
