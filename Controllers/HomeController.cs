namespace ConnectFour.Controllers;

using Microsoft.AspNetCore.Mvc;
using ConnectFour.Models;
using ConnectFour.Services;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly RoomManager _roomManager;

    public HomeController(ILogger<HomeController> logger, RoomManager roomManager)
    {
        _logger = logger;
        _roomManager = roomManager;
    }

    public IActionResult Index()
    {
        _logger.LogInformation("Home/Index accessed");
        return View();
    }

    [HttpGet("api/rooms")]
    public IActionResult GetRooms()
    {
        try
        {
            var rooms = _roomManager.GetAllRooms()
                .Where(r => r.Status == RoomStatus.Waiting || r.Status == RoomStatus.Ready)
                .Select(r => new
                {
                    roomId = r.RoomId.ToString(),
                    player1Nick = r.Player1Nick,
                    player2Nick = r.Player2Nick,
                    status = r.Status.ToString(),
                    createdAt = r.CreatedAt
                })
                .ToList();

            _logger.LogInformation("Returning {Count} rooms", rooms.Count);
            return Ok(rooms);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting rooms");
            return StatusCode(500, new { error = "Błąd pobierania pokoi" });
        }
    }

}
