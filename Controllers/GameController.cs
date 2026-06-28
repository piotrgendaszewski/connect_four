namespace ConnectFour.Controllers;

using Microsoft.AspNetCore.Mvc;
using ConnectFour.Models;
using ConnectFour.Services;

public class GameController : Controller
{
    private readonly ILogger<GameController> _logger;
    private readonly RoomManager _roomManager;

    public GameController(ILogger<GameController> logger, RoomManager roomManager)
    {
        _logger = logger;
        _roomManager = roomManager;
    }

    [HttpPost("Game/Create")]
    public async Task<IActionResult> Create([FromBody] CreateRoomRequest request)
    {
        _logger.LogInformation("Create room request: nick={Nick}", request.Nick);

        if (!ModelState.IsValid)
        {
                _logger.LogWarning("Invalid model state for Create: {Errors}", string.Join(", ", ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage))));
            return BadRequest(ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage)));
        }

        try
        {
            var room = await _roomManager.CreateRoomAsync(request.Nick);
            _logger.LogInformation("Room created successfully: roomId={RoomId}", room.RoomId);

            return Ok(new { roomId = room.RoomId.ToString() });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "No available rooms");
            return StatusCode(503, new { error = "Brak wolnych miejsc. Spróbuj ponownie później." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating room");
            return StatusCode(500, new { error = "Błąd podczas tworzenia pokoju" });
        }
    }

    [HttpPost("Game/Join")]
    public IActionResult Join([FromBody] JoinRoomRequest request)
    {
        _logger.LogInformation("Join room request: roomId={RoomId}, nick={Nick}", request.RoomId, request.Nick);

        if (!ModelState.IsValid)
        {
            _logger.LogWarning("Invalid model state for Join");
            return BadRequest(ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage)));
        }

        if (!Guid.TryParse(request.RoomId, out var roomGuid))
        {
            _logger.LogWarning("Invalid GUID format for roomId: {RoomId}", request.RoomId);
            return BadRequest(new { error = "Nieprawidłowy format ID pokoju" });
        }

        try
        {
            var room = _roomManager.GetRoom(request.RoomId);
            if (room == null)
            {
                _logger.LogWarning("Room not found: {RoomId}", request.RoomId);
                TempData["Error"] = "Pokój nie znaleziony";
                return RedirectToAction("Index", "Home");
            }

            if (room.Status != RoomStatus.Waiting)
            {
                _logger.LogWarning("Room {RoomId} is not waiting", request.RoomId);
                TempData["Error"] = "Pokój nie jest dostępny";
                return RedirectToAction("Index", "Home");
            }

            if (request.Nick == room.Player1Nick)
            {
                _logger.LogWarning("Nick {Nick} is already player 1 in room {RoomId}", request.Nick, request.RoomId);
                TempData["Error"] = "Ten nick jest już zajęty w tym pokoju";
                return RedirectToAction("Index", "Home");
            }

            var joinedRoom = _roomManager.JoinRoom(request.RoomId, request.Nick, "");
            if (joinedRoom == null)
            {
                _logger.LogWarning("Failed to join room {RoomId}", request.RoomId);
                TempData["Error"] = "Nie można dołączyć do pokoju";
                return RedirectToAction("Index", "Home");
            }

            _logger.LogInformation("Player {Nick} joined room {RoomId}", request.Nick, request.RoomId);
            return Ok(new { roomId = request.RoomId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error joining room");
            return StatusCode(500, new { error = "Błąd podczas dołączania do pokoju" });
        }
    }

    [HttpGet("Game/Play/{roomId}")]
    public IActionResult Play(string roomId)
    {
        _logger.LogInformation("Play page accessed: roomId={RoomId}", roomId);

        if (!Guid.TryParse(roomId, out _))
        {
            _logger.LogWarning("Invalid GUID format for roomId: {RoomId}", roomId);
            return RedirectToAction("Index", "Home");
        }

        var room = _roomManager.GetRoom(roomId);
        if (room == null)
        {
            _logger.LogWarning("Room not found: {RoomId}", roomId);
            return RedirectToAction("Index", "Home");
        }

        var nick = HttpContext.Request.Query["nick"].ToString();
        if (string.IsNullOrEmpty(nick))
        {
            _logger.LogWarning("No nick provided for room {RoomId}", roomId);
            return RedirectToAction("Index", "Home");
        }

        int playerNumber = room.Player1Nick == nick ? 1 : (room.Player2Nick == nick ? 2 : 0);
        if (playerNumber == 0)
        {
            _logger.LogWarning("Player {Nick} is not in room {RoomId}", nick, roomId);
            return RedirectToAction("Index", "Home");
        }

        ViewBag.RoomId = roomId;
        ViewBag.Nick = nick;
        ViewBag.PlayerNumber = playerNumber;

        _logger.LogInformation("Play page loaded: roomId={RoomId}, nick={Nick}, playerNumber={PlayerNumber}", roomId, nick, playerNumber);
        return View();
    }
}
