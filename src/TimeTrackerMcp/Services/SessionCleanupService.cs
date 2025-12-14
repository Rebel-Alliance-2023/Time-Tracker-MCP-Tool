using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TimeTrackerMcp.Services;

/// <summary>
/// Background service that periodically cleans up expired sessions.
/// </summary>
public class SessionCleanupService : IHostedService, IDisposable
{
    private readonly ISessionService _sessionService;
    private readonly ILogger<SessionCleanupService> _logger;
    private Timer? _timer;
    
    // M2-030: Cleanup interval (5 minutes)
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(5);

    public SessionCleanupService(
        ISessionService sessionService,
        ILogger<SessionCleanupService> logger)
    {
        _sessionService = sessionService;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Session cleanup service starting. Interval: {Interval} minutes", 
            CleanupInterval.TotalMinutes);

        // Start timer with cleanup interval
        _timer = new Timer(
            callback: DoCleanup,
            state: null,
            dueTime: CleanupInterval, // First run after interval
            period: CleanupInterval);  // Then repeat at interval

        return Task.CompletedTask;
    }

    private void DoCleanup(object? state)
    {
        try
        {
            var cleanedCount = _sessionService.CleanupExpiredSessions();
            
            if (cleanedCount > 0)
            {
                _logger.LogInformation(
                    "Session cleanup completed. Removed {Count} expired session(s). Active sessions: {ActiveCount}",
                    cleanedCount,
                    _sessionService.SessionCount);
            }
            else
            {
                _logger.LogDebug(
                    "Session cleanup completed. No expired sessions. Active sessions: {ActiveCount}",
                    _sessionService.SessionCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during session cleanup");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Session cleanup service stopping");

        _timer?.Change(Timeout.Infinite, 0);

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }
}
