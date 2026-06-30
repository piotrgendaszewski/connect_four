namespace ConnectFour.Hubs;

using Microsoft.AspNetCore.SignalR;
using ConnectFour.Models;
using ConnectFour.Services;

public class GameHub : Hub
{
    private readonly RoomManager _roomManager;
    private readonly ILogger<GameHub> _logger;

    public GameHub(RoomManager roomManager, ILogger<GameHub> logger)
    {
        _roomManager = roomManager;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client {ConnectionId} connected", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client {ConnectionId} disconnected", Context.ConnectionId);

        var room = _roomManager.GetAllRooms()
            .FirstOrDefault(r => r.Player1ConnectionId == Context.ConnectionId || r.Player2ConnectionId == Context.ConnectionId);

        if (room != null)
        {
            var disconnectedPlayerNick = room.Player1ConnectionId == Context.ConnectionId ? room.Player1Nick : room.Player2Nick;
            _logger.LogInformation("Player {Nick} from room {RoomId} disconnected", disconnectedPlayerNick, room.RoomId);

            room.Status = RoomStatus.Finished;
            await Clients.Group(room.RoomId.ToString()).SendAsync("PlayerLeft", new { nick = disconnectedPlayerNick });
            _roomManager.RemoveRoom(room.RoomId.ToString());
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task JoinRoom(string roomId, string nick)
    {
        _logger.LogInformation("JoinRoom called: roomId={RoomId}, nick={Nick}, connectionId={ConnectionId}", 
            roomId, nick, Context.ConnectionId);

        try
        {
            var room = _roomManager.GetRoom(roomId);
            if (room == null)
            {
                _logger.LogWarning("Room {RoomId} not found", roomId);
                await Clients.Caller.SendAsync("MoveError", new { message = "Pokój nie znaleziony" });
                return;
            }

            // Jeśli to gracz 1 (join/reconnect)
            if (room.Player1Nick == nick)
            {
                room.Player1ConnectionId = Context.ConnectionId;
                _logger.LogInformation("Player1 {Nick} connected/reconnected to room {RoomId}", nick, roomId);
            }
            // Jeśli to gracz 2
            else if (room.Player2Nick == null)
            {
                var joinedRoom = _roomManager.JoinRoom(roomId, nick, Context.ConnectionId);
                if (joinedRoom == null)
                {
                    _logger.LogWarning("Failed to join room {RoomId}", roomId);
                    await Clients.Caller.SendAsync("MoveError", new { message = "Nie można dołączyć do pokoju" });
                    return;
                }
                room = joinedRoom;
                _logger.LogInformation("Player2 {Nick} joined room {RoomId}", nick, roomId);
            }
            else if (room.Player2Nick == nick)
            {
                room.Player2ConnectionId = Context.ConnectionId;
                _logger.LogInformation("Player2 {Nick} connected/reconnected to room {RoomId}", nick, roomId);
            }
            else
            {
                _logger.LogWarning("Cannot join room {RoomId}: invalid state", roomId);
                await Clients.Caller.SendAsync("MoveError", new { message = "Nie można dołączyć do pokoju" });
                return;
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, roomId);
            room.LastActivityAt = DateTime.UtcNow;

            int playerNumber = room.Player1Nick == nick ? 1 : 2;
            await Clients.Group(roomId).SendAsync("PlayerJoined", new 
            { 
                nick, 
                playerNumber,
                player1Nick = room.Player1Nick,
                player2Nick = room.Player2Nick,
                status = room.Status.ToString()
            });

            _logger.LogInformation("Player {Nick} (player {PlayerNumber}) successfully joined/rejoined room {RoomId}", 
                nick, playerNumber, roomId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in JoinRoom");
            await Clients.Caller.SendAsync("MoveError", new { message = "Błąd podczas dołączania" });
        }
    }

    public async Task SignalReady(string roomId, string nick)
    {
        _logger.LogInformation("SignalReady called: roomId={RoomId}, nick={Nick}", roomId, nick);

        try
        {
            var room = _roomManager.GetRoom(roomId);
            if (room == null)
            {
                await Clients.Caller.SendAsync("MoveError", new { message = "Pokój nie znaleziony" });
                return;
            }

            int playerNumber = room.Player1Nick == nick ? 1 : 2;
            bool success = await _roomManager.SignalReadyAsync(roomId, playerNumber);

            if (!success)
            {
                await Clients.Group(roomId).SendAsync("MoveError", new { message = "Błąd synchronizacji" });
                return;
            }

            if (room.Status == RoomStatus.InProgress)
            {
                _logger.LogInformation("Game starting in room {RoomId}", roomId);
                await Clients.Group(roomId).SendAsync("GameStart", new
                {
                    roomId,
                    player1Nick = room.Player1Nick,
                    player2Nick = room.Player2Nick,
                    currentTurn = room.CurrentTurn
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SignalReady");
            await Clients.Caller.SendAsync("MoveError", new { message = "Błąd podczas sygnalizacji gotowości" });
        }
    }

    public async Task MakeMove(string roomId, int column, int playerNumber)
    {
        _logger.LogInformation("MakeMove called: roomId={RoomId}, column={Column}, playerNumber={PlayerNumber}", 
            roomId, column, playerNumber);

        try
        {
            var roomForPlayerResolve = _roomManager.GetRoom(roomId);
            if (roomForPlayerResolve == null)
            {
                await Clients.Caller.SendAsync("MoveError", new { message = "Pokój nie znaleziony" });
                return;
            }

            int resolvedPlayerNumber = roomForPlayerResolve.Player1ConnectionId == Context.ConnectionId
                ? 1
                : roomForPlayerResolve.Player2ConnectionId == Context.ConnectionId
                    ? 2
                    : 0;

            if (resolvedPlayerNumber == 0)
            {
                _logger.LogWarning("Connection {ConnectionId} not mapped in room {RoomId}, using client playerNumber fallback={PlayerNumber}", Context.ConnectionId, roomId, playerNumber);
                if (playerNumber is 1 or 2)
                {
                    resolvedPlayerNumber = playerNumber;
                }
                else
                {
                    await Clients.Caller.SendAsync("MoveError", new { message = "Nieprawidłowy gracz" });
                    return;
                }
            }

            var result = _roomManager.MakeMove(roomId, column, resolvedPlayerNumber);

            if (!result.Success)
            {
                _logger.LogWarning("Move failed: {Error}", result.Error);
                await Clients.Caller.SendAsync("MoveError", new { message = result.Error });
                return;
            }

            var room = _roomManager.GetRoom(roomId);
            if (room == null)
            {
                await Clients.Caller.SendAsync("MoveError", new { message = "Pokój nie znaleziony" });
                return;
            }

            var board = ToJagged(room.Board.Cells);
            await Clients.Group(roomId).SendAsync("MoveResult", new
            {
                row = result.Row,
                column = result.Column,
                playerNumber = result.PlayerNumber,
                nextTurn = result.NextTurn,
                board = board
            });

            if (result.IsWin || result.IsDraw)
            {
                string? winnerNick = result.IsWin ? 
                    (resolvedPlayerNumber == 1 ? room.Player1Nick : room.Player2Nick) : null;

                await Clients.Group(roomId).SendAsync("GameOver", new
                {
                    winnerNick,
                    isDraw = result.IsDraw,
                    winningCells = result.WinningCells.Select(wc => new { row = wc.Row, col = wc.Col }).ToList()
                });

                _logger.LogInformation("Game finished in room {RoomId}: winner={Winner}, draw={IsDraw}", 
                    roomId, winnerNick, result.IsDraw);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in MakeMove");
            await Clients.Caller.SendAsync("MoveError", new { message = "Błąd podczas wykonywania ruchu" });
        }
    }

    public async Task RequestRematch(string roomId, string nick)
    {
        _logger.LogInformation("RequestRematch called: roomId={RoomId}, nick={Nick}", roomId, nick);

        try
        {
            var room = _roomManager.GetRoom(roomId);
            if (room == null)
            {
                await Clients.Caller.SendAsync("MoveError", new { message = "Pokój nie znaleziony" });
                return;
            }

            int playerNumber = room.Player1Nick == nick ? 1 : (room.Player2Nick == nick ? 2 : 0);
            if (playerNumber == 0)
            {
                await Clients.Caller.SendAsync("MoveError", new { message = "Nieprawidłowy gracz" });
                return;
            }

            bool success = _roomManager.RequestRematch(roomId, playerNumber, out bool bothRequested);
            if (!success)
            {
                await Clients.Caller.SendAsync("MoveError", new { message = "Nie można rozpocząć rewanżu" });
                return;
            }

            room = _roomManager.GetRoom(roomId);
            if (room == null)
                return;

            if (!bothRequested)
            {
                await Clients.Group(roomId).SendAsync("RematchRequested", new
                {
                    requestedBy = nick,
                    player1RematchRequested = room.Player1RematchRequested,
                    player2RematchRequested = room.Player2RematchRequested
                });
            }
            else
            {
                await Clients.Group(roomId).SendAsync("RematchStarted", new
                {
                    currentTurn = room.CurrentTurn,
                    board = ToJagged(room.Board.Cells),
                    player1Nick = room.Player1Nick,
                    player2Nick = room.Player2Nick
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in RequestRematch");
            await Clients.Caller.SendAsync("MoveError", new { message = "Błąd podczas rewanżu" });
        }
    }

    private static int[][] ToJagged(int[,] board)
    {
        int rows = board.GetLength(0);
        int cols = board.GetLength(1);
        var result = new int[rows][];
        for (int r = 0; r < rows; r++)
        {
            result[r] = new int[cols];
            for (int c = 0; c < cols; c++)
                result[r][c] = board[r, c];
        }
        return result;
    }
}
