namespace AspApp.Models;

public class User_Profile_Model
{
    public Guid Id { get; set; }
    public string Username { get; set; } = "";
    public string Email { get; set; } = "";
    public bool EmailConfirmed { get; set; } = false;
    public string? DisplayName { get; set; }
    public string? Description { get; set; }
    public bool PublicEmail { get; set; } = false;
    public int ImageVersion { get; set; }
    public bool HasImage { get; set; } = false;
    public string? RemoteImageUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public string[] Roles { get; set; } = [];
}