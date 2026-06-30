using Xunit;
using ConnectFour.Models;
using ConnectFour.Services;

public class GameEngineTests
{
    [Fact]
    public void CheckWin_ShouldDetectHorizontalWin()
    {
        var engine = new GameEngine();
        var board = new GameBoard();

        board.Cells[5, 0] = 1;
        board.Cells[5, 1] = 1;
        board.Cells[5, 2] = 1;
        board.Cells[5, 3] = 1;

        var result = engine.CheckWin(board, 1, 5, 3);

        Assert.True(result.IsWin);
        Assert.True(result.WinningCells.Count >= 4);
    }

    [Fact]
    public void CheckDraw_ShouldReturnTrue_WhenTopRowIsFull()
    {
        var engine = new GameEngine();
        var board = new GameBoard();

        for (var col = 0; col < GameBoard.Cols; col++)
        {
            board.Cells[0, col] = 1;
        }

        var isDraw = engine.CheckDraw(board);

        Assert.True(isDraw);
    }
}
