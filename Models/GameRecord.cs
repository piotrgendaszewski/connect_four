namespace ConnectFour.Models;

public class GameRecord
{
    public int Id { get; set; }
    public string Player1Nick { get; set; } = string.Empty;
    public string Player2Nick { get; set; } = string.Empty;
    public string? WinnerNick { get; set; }
    public int DurationSeconds { get; set; }
    public DateTime PlayedAt { get; set; }
}
