namespace ConnectFour.Models;

public class GameBoard
{
    public const int Rows = 6;
    public const int Cols = 7;
    public int[,] Cells { get; set; } = new int[Rows, Cols];
}
