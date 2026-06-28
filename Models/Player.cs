namespace ConnectFour.Models;

public class Player
{
    public int Id { get; set; }
    public string Nick { get; set; } = string.Empty;
    public int Wins { get; set; }
    public int Losses { get; set; }
    public int Draws { get; set; }
}
