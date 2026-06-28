namespace ConnectFour.Services;

using ConnectFour.Data;
using ConnectFour.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using ConnectFour.Hubs;

public class BackgroundTaskService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BackgroundTaskService> _logger;
    private List<Player>? _rankingCache;

    public BackgroundTaskService(IServiceProvider serviceProvider, ILogger<BackgroundTaskService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BackgroundTaskService started");

        var tasks = new List<Task>
        {
            CleanupLoopAsync(stoppingToken),
            RankingCacheRefreshAsync(stoppingToken),
            WebSocketPingAsync(stoppingToken)
        };

        await Task.WhenAll(tasks);
    }

    private async Task CleanupLoopAsync(CancellationToken stoppingToken)
    {
        using (var scope = _serviceProvider.CreateScope())
        {
            var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
            var roomManager = scope.ServiceProvider.GetRequiredService<RoomManager>();
            int intervalSeconds = config.GetValue<int>("Game:InactiveRoomCleanupIntervalSeconds", 60);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);

                    ThreadPool.QueueUserWorkItem(_ =>
                    {
                        try
                        {
                            int removed = roomManager.CleanupInactiveRooms(TimeSpan.FromSeconds(30));
                            _logger.LogInformation("Cleaned up {Count} inactive rooms", removed);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error during cleanup");
                        }
                    });
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in cleanup loop");
                }
            }
        }
    }

    private async Task RankingCacheRefreshAsync(CancellationToken stoppingToken)
    {
        using (var scope = _serviceProvider.CreateScope())
        {
            var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
            int intervalSeconds = config.GetValue<int>("Game:RankingCacheRefreshSeconds", 30);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);

                    await Task.Run(async () =>
                    {
                        try
                        {
                            using (var innerScope = _serviceProvider.CreateScope())
                            {
                                var db = innerScope.ServiceProvider.GetRequiredService<AppDbContext>();
                                var ranking = await db.Players
                                    .OrderByDescending(p => p.Wins)
                                    .Take(10)
                                    .ToListAsync(stoppingToken);
                                _rankingCache = ranking;
                                _logger.LogInformation("Ranking cache refreshed with {Count} players", ranking.Count);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error refreshing ranking cache");
                        }
                    });
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in ranking refresh loop");
                }
            }
        }
    }

    private async Task WebSocketPingAsync(CancellationToken stoppingToken)
    {
        using (var scope = _serviceProvider.CreateScope())
        {
            var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
            var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<GameHub>>();
            int intervalSeconds = config.GetValue<int>("Game:WebSocketPingIntervalSeconds", 10);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
                    await hubContext.Clients.All.SendAsync("Ping", new { serverTimeUtc = DateTime.UtcNow }, stoppingToken);
                    _logger.LogDebug("WebSocket ping sent");
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in WebSocket ping");
                }
            }
        }
    }

    public List<Player>? GetCachedRanking() => _rankingCache;
}
