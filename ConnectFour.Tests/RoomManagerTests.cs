using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using ConnectFour.Models;
using ConnectFour.Services;

public class RoomManagerTests
{
    [Fact]
    public async Task SignalReadyAsync_ShouldStartGame_AfterBothPlayersReady()
    {
        var roomManager = CreateRoomManager();
        var room = await roomManager.CreateRoomAsync("P1");
        roomManager.JoinRoom(room.RoomId.ToString(), "P2", "conn2");

        var firstReady = await roomManager.SignalReadyAsync(room.RoomId.ToString(), 1);
        var secondReady = await roomManager.SignalReadyAsync(room.RoomId.ToString(), 2);
        var updatedRoom = roomManager.GetRoom(room.RoomId.ToString());

        Assert.True(firstReady);
        Assert.True(secondReady);
        Assert.NotNull(updatedRoom);
        Assert.Equal(RoomStatus.InProgress, updatedRoom!.Status);
        Assert.Contains(updatedRoom.CurrentTurn, new[] { 1, 2 });
    }

    [Fact]
    public async Task RequestRematch_ShouldResetBoard_WhenBothPlayersRequest()
    {
        var roomManager = CreateRoomManager();
        var room = await roomManager.CreateRoomAsync("P1");
        var roomId = room.RoomId.ToString();
        roomManager.JoinRoom(roomId, "P2", "conn2");

        var existingRoom = roomManager.GetRoom(roomId)!;
        existingRoom.Status = RoomStatus.Finished;
        existingRoom.Board.Cells[5, 0] = 1;
        existingRoom.Board.Cells[5, 1] = 2;

        var firstRequest = roomManager.RequestRematch(roomId, 1, out var bothAfterFirst);
        var secondRequest = roomManager.RequestRematch(roomId, 2, out var bothAfterSecond);

        var updatedRoom = roomManager.GetRoom(roomId);

        Assert.True(firstRequest);
        Assert.False(bothAfterFirst);
        Assert.True(secondRequest);
        Assert.True(bothAfterSecond);
        Assert.NotNull(updatedRoom);
        Assert.Equal(RoomStatus.InProgress, updatedRoom!.Status);
        Assert.Contains(updatedRoom.CurrentTurn, new[] { 1, 2 });

        for (var row = 0; row < GameBoard.Rows; row++)
        {
            for (var col = 0; col < GameBoard.Cols; col++)
            {
                Assert.Equal(0, updatedRoom.Board.Cells[row, col]);
            }
        }
    }

    private static RoomManager CreateRoomManager()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Game:MaxActiveRooms"] = "10",
                ["Game:PlayerReadyTimeoutSeconds"] = "60"
            })
            .Build();

        return new RoomManager(config, new GameEngine(), NullLogger<RoomManager>.Instance);
    }
}
