using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using var services = new ServiceCollection()
    .AddLogging(builder => builder
        .AddConsole()
        .AddSeq())
    .BuildServiceProvider();

var logger = services.GetRequiredService<ILogger<Program>>();

logger.LogInformation("Hello, {Name}!", Environment.UserName);
