using Projects;

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

IResourceBuilder<PostgresDatabaseResource> postgres = builder.AddPostgres("postgres",
        builder.CreateResourceBuilder(new ParameterResource("username",
            _ => "postgres")),
        builder.CreateResourceBuilder(new ParameterResource("password",
            _ => "Password12!")),
        5432)
    .AddDatabase("JobService", "JobService");

builder.AddProject<JobService_Service>("JobServiceService")
    .WithReference(postgres);
builder.Build().Run();