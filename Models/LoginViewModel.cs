using System.ComponentModel.DataAnnotations;

namespace ResidentPortal.Models;

public class LoginViewModel
{
    [Required(ErrorMessage = "Username is required.")]
    [StringLength(100)]
    [Display(Name = "Username")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [StringLength(200, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters.")]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;
}
