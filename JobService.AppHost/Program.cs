using Projects;

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres",
        builder.CreateResourceBuilder(new ParameterResource("username",
            _ => "postgres")),
        builder.CreateResourceBuilder(new ParameterResource("password",
            _ => "Password12!")),
        5432)
    .WithDataBindMount("./pgdata")
    .AddDatabase("JobService", "JobService");

var rabbitMq = builder.AddRabbitMQ("RabbitMq",
    builder.CreateResourceBuilder<ParameterResource>(
        new ParameterResource("rmqUsername", _ => "guest")),
    builder.CreateResourceBuilder<ParameterResource>(
        new ParameterResource("rmqPassword", _ => "guest")),
    5672)
    .WithEndpoint(15672, 15672, scheme:"http", name: "rmqManagement")
    .WithImage("masstransit/rabbitmq");

var project = builder.AddProject<JobService_Service>("JobServiceService")
    .WithReference(postgres)
    .WithReference(rabbitMq);

project.WaitFor(postgres)
    .WaitFor(rabbitMq);

builder.Build().Run();