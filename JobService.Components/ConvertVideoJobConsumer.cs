using MassTransit;

using Microsoft.Extensions.Logging;

namespace JobService.Components;

public class ConvertVideoJobConsumer : IJobConsumer<ConvertVideo>
{
    private readonly ILogger<ConvertVideoJobConsumer> _logger;

    public ConvertVideoJobConsumer(ILogger<ConvertVideoJobConsumer> logger)
    {
        _logger = logger;
    }

    public async Task Run(JobContext<ConvertVideo> context)
    {
        Random rng = new Random();

        TimeSpan variance = TimeSpan.FromMilliseconds(rng.Next(8399, 28377));

        _logger.LogInformation("Converting Video: {GroupId} {Path}", context.Job.GroupId, context.Job.Path);

        await Task.Delay(variance);

        await context.Publish<VideoConverted>(context.Job);

        _logger.LogInformation("Converted Video: {GroupId} {Path}", context.Job.GroupId, context.Job.Path);
    }
}