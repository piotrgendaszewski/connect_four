namespace ConnectFour.Services;

public class MoveResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int Row { get; set; }
    public int Column { get; set; }
    public int PlayerNumber { get; set; }
    public bool IsWin { get; set; }
    public bool IsDraw { get; set; }
    public List<(int Row, int Col)> WinningCells { get; set; } = new();
    public int NextTurn { get; set; }
}
