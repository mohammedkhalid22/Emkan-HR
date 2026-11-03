using System.ComponentModel.DataAnnotations;

namespace HRSystem.Web.ViewModels;

public class ChangePasswordViewModel
{
    [Display(Name = "كلمة السر الحالية")]
    [Required(ErrorMessage = "{0} مطلوبة")]
    [DataType(DataType.Password)]
    public string CurrentPassword { get; set; } = null!;

    [Display(Name = "كلمة السر الجديدة")]
    [Required(ErrorMessage = "{0} مطلوبة")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "{0} لازم تكون {2} حروف على الأقل")]
    [DataType(DataType.Password)]
    public string NewPassword { get; set; } = null!;

    [Display(Name = "أكد كلمة السر الجديدة")]
    [Compare(nameof(NewPassword), ErrorMessage = "كلمتين السر مش زي بعض")]
    [DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = null!;
}

public class ForgotPasswordViewModel
{
    [Display(Name = "البريد الإلكتروني")]
    [Required(ErrorMessage = "{0} مطلوب")]
    [EmailAddress(ErrorMessage = "{0} مش صحيح")]
    public string Email { get; set; } = null!;
}

public class ResetPasswordViewModel
{
    public string Email { get; set; } = null!;
    public string Token { get; set; } = null!;

    [Display(Name = "كلمة السر الجديدة")]
    [Required(ErrorMessage = "{0} مطلوبة")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "{0} لازم تكون {2} حروف على الأقل")]
    [DataType(DataType.Password)]
    public string NewPassword { get; set; } = null!;

    [Display(Name = "أكد كلمة السر الجديدة")]
    [Compare(nameof(NewPassword), ErrorMessage = "كلمتين السر مش زي بعض")]
    [DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = null!;
}

// The "my account" page: password and two-step sign-in
public class SecurityViewModel
{
    public string Email { get; set; } = null!;
    public bool TwoFactorEnabled { get; set; }
    public ChangePasswordViewModel ChangePassword { get; set; } = new();
}

public class TwoFactorSetupViewModel
{
    // Shown only; nullable so the posted form (which carries just the code) still validates
    public string? SharedKey { get; set; }
    public string? AuthenticatorUri { get; set; }

    [Display(Name = "الكود اللي ظاهر في التطبيق")]
    [Required(ErrorMessage = "{0} مطلوب")]
    [StringLength(7, MinimumLength = 6, ErrorMessage = "الكود 6 أرقام")]
    public string Code { get; set; } = null!;
}

public class TwoFactorLoginViewModel
{
    [Display(Name = "الكود من تطبيق التحقق")]
    [Required(ErrorMessage = "{0} مطلوب")]
    public string Code { get; set; } = null!;

    [Display(Name = "افتكر الجهاز ده 30 يوم")]
    public bool RememberMachine { get; set; }

    public bool RememberMe { get; set; }
    public string? ReturnUrl { get; set; }
}
