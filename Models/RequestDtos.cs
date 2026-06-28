namespace ConnectFour.Models;

using System.ComponentModel.DataAnnotations;

public class CreateRoomRequest
{
    [Required]
    [RegularExpression("^[a-zA-Z0-9]{3,20}$")]
    public string Nick { get; set; } = string.Empty;
}

public class JoinRoomRequest
{
    [Required]
    public string RoomId { get; set; } = string.Empty;

    [Required]
    [RegularExpression("^[a-zA-Z0-9]{3,20}$")]
    public string Nick { get; set; } = string.Empty;
}
