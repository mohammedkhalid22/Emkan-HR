using System.ComponentModel.DataAnnotations;

namespace HRSystem.Web.ViewModels;

public class LoginViewModel
{
    [Display(Name = "البريد الإلكتروني")]
    [Required(ErrorMessage = "{0} مطلوب")]
    [EmailAddress(ErrorMessage = "{0} مش صحيح")]
    public string Email { get; set; } = null!;

    [Display(Name = "كلمة السر")]
    [Required(ErrorMessage = "{0} مطلوبة")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = null!;

    [Display(Name = "افتكرني")]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}
