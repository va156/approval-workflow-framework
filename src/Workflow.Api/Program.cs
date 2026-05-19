using Workflow.Abstractions;
using Workflow.Engine;
using Workflow.Persistence.EFCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var connectionString = builder.Configuration.GetConnectionString("WorkflowPrimary")
    ?? "Server=(localdb)\\MSSQLLocalDB;Database=WorkflowEngine;Trusted_Connection=True;TrustServerCertificate=True";

builder.Services.AddWorkflowPersistenceSqlServer(connectionString);
builder.Services.AddWorkflowEngine();
builder.Services.AddSingleton<IGroupProvider>(new StaticGroupProvider(new Dictionary<string, List<string>>
{
    ["HR_GROUP"] = ["user.hr.1", "user.hr.2"],
    ["TEAMLEAD_GROUP"] = ["user.lead.1", "user.lead.2"]
}));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.Run();
