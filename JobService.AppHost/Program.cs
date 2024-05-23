using Projects;

var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<JobService_Service>("JobService");
builder.Build().Run();
