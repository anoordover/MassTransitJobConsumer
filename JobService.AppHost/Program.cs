using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres",
        builder.CreateResourceBuilder<ParameterResource>(new ParameterResource("username",
            _ => "postgres")),
        builder.CreateResourceBuilder<ParameterResource>(new ParameterResource("password",
            _ => "Password12!")),
        5432)
    .AddDatabase("JobService", "JobService");

builder.AddProject<JobService_Service>("JobServiceService")
    .WithReference(postgres);
builder.Build().Run();
