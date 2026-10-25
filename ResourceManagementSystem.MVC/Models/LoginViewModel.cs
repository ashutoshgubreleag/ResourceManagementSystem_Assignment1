using System.ComponentModel.DataAnnotations;

namespace ResourceManagementSystem.MVC.Models;

public sealed class LoginViewModel
{
    [Required, EmailAddress, StringLength(120)]
    public string Email { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), StringLength(128)]
    public string Password { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}
