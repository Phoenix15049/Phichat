using Microsoft.Extensions.Logging;
using Phichat.Application.Interfaces;
using Phichat.Infrastructure.Data;

namespace Phichat.Infrastructure.Services;

public class ConsoleSmsSender : ISmsSender
{
    private readonly ILogger<ConsoleSmsSender> _logger;

    public ConsoleSmsSender(ILogger<ConsoleSmsSender> logger)
    {
        _logger = logger;
    }


    public Task SendAsync(string phoneNumber, string message)
    {
        Console.WriteLine($"[SMS to {phoneNumber}] {message}");
        _logger.LogInformation($"[SMS to {phoneNumber}] {message}");
        return Task.CompletedTask;
    }
}
