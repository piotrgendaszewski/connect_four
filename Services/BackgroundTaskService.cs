namespace ConnectFour.Services;

using Microsoft.AspNetCore.SignalR;
using ConnectFour.Hubs;
using ConnectFour.Models;

public class BackgroundTaskService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BackgroundTaskService> _logger;

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
            var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<GameHub>>();
            int intervalSeconds = config.GetValue<int>("Game:InactiveRoomCleanupIntervalSeconds", 60);
            var timeout = TimeSpan.FromSeconds(30);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);

                    var now = DateTime.UtcNow;
                    var inactiveRooms = roomManager.GetAllRooms()
                        .Where(r => (now - r.LastActivityAt) > timeout)
                        .ToList();

                    foreach (var room in inactiveRooms)
                    {
                        room.Status = RoomStatus.Finished;
                        var roomId = room.RoomId.ToString();
                        var activeConnectionIds = new[] { room.Player1ConnectionId, room.Player2ConnectionId }
                            .Where(id => !string.IsNullOrWhiteSpace(id))
                            .Distinct()
                            .ToList();

                        await hubContext.Clients.Group(roomId).SendAsync("RoomClosed", new
                        {
                            message = "Pokój zosta³ zamkniêty z powodu braku aktywnoœci."
                        }, stoppingToken);

                        if (activeConnectionIds.Count > 0)
                        {
                            await hubContext.Clients.Clients(activeConnectionIds).SendAsync("RoomClosed", new
                            {
                                message = "Pokój zosta³ zamkniêty z powodu braku aktywnoœci."
                            }, stoppingToken);
                        }

                        roomManager.RemoveRoom(roomId);
                    }

                    if (inactiveRooms.Count > 0)
                    {
                        _logger.LogInformation("Cleaned up {Count} inactive rooms", inactiveRooms.Count);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during cleanup");
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

}
