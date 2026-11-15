using System.ComponentModel.DataAnnotations;

namespace BeanSceneReservationSystem.Models;

public class LoginViewModel
{
    [Required(ErrorMessage = "Please select a profile.")]
    [RegularExpression("^(Manager|Staff|Member)$", ErrorMessage = "Please select a valid profile.")]
    [Display(Name = "Profile")]
    public string SelectedRole { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Remember me")]
    public bool RememberMe { get; set; }
}
