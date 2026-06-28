namespace ConnectFour.Models;

public enum RoomStatus { Waiting, Ready, InProgress, Finished }

public class GameRoom
{
    public Guid RoomId { get; set; } = Guid.NewGuid();
    public string? Player1Nick { get; set; }
    public string? Player2Nick { get; set; }
    public string? Player1ConnectionId { get; set; }
    public string? Player2ConnectionId { get; set; }
    public GameBoard Board { get; set; } = new GameBoard();
    public int CurrentTurn { get; set; } = 1;
    public RoomStatus Status { get; set; } = RoomStatus.Waiting;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;
    public bool Player1Ready { get; set; } = false;
    public bool Player2Ready { get; set; } = false;
    public bool Player1RematchRequested { get; set; } = false;
    public bool Player2RematchRequested { get; set; } = false;
    public DateTime? GameStartedAt { get; set; }

    public readonly object BoardLock = new object();
}
