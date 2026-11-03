using System.Net;
using System.Text;
using HRSystem.BLL.Common;
using HRSystem.Domain.Identity;
using HRSystem.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace HRSystem.Web.Controllers;

public class AccountController : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailSender _email;
    private readonly ILogger<AccountController> _logger;

    public AccountController(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager,
        IEmailSender email, ILogger<AccountController> logger)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _email = email;
        _logger = logger;
    }

    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Home");

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user is null || !user.IsActive)
        {
            ModelState.AddModelError(string.Empty, "البريد الإلكتروني أو كلمة السر غلط");
            return View(model);
        }

        var result = await _signInManager.PasswordSignInAsync(user, model.Password, model.RememberMe, lockoutOnFailure: true);

        if (result.Succeeded)
            return Url.IsLocalUrl(model.ReturnUrl) ? Redirect(model.ReturnUrl) : RedirectToAction("Index", "Home");

        if (result.RequiresTwoFactor)
            return RedirectToAction(nameof(LoginWith2fa), new { model.RememberMe, model.ReturnUrl });

        ModelState.AddModelError(string.Empty, result.IsLockedOut
            ? "الحساب اتقفل مؤقتًا بسبب محاولات كتير غلط. جرب بعد 15 دقيقة"
            : "البريد الإلكتروني أو كلمة السر غلط");
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    public IActionResult AccessDenied() => View();

    // ---------- Two-step sign-in: the second step ----------

    [AllowAnonymous]
    public async Task<IActionResult> LoginWith2fa(bool rememberMe, string? returnUrl)
    {
        // Only reachable right after a correct password
        if (await _signInManager.GetTwoFactorAuthenticationUserAsync() is null)
            return RedirectToAction(nameof(Login));
        return View(new TwoFactorLoginViewModel { RememberMe = rememberMe, ReturnUrl = returnUrl });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LoginWith2fa(TwoFactorLoginViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);
        if (await _signInManager.GetTwoFactorAuthenticationUserAsync() is null)
            return RedirectToAction(nameof(Login));

        var code = model.Code.Replace(" ", "").Replace("-", "");

        // 6 digits = the app's code; anything longer = one of the saved recovery codes
        var result = code.Length <= 7
            ? await _signInManager.TwoFactorAuthenticatorSignInAsync(code, model.RememberMe, model.RememberMachine)
            : await _signInManager.TwoFactorRecoveryCodeSignInAsync(model.Code.Trim());

        if (result.Succeeded)
            return Url.IsLocalUrl(model.ReturnUrl) ? Redirect(model.ReturnUrl) : RedirectToAction("Index", "Home");

        ModelState.AddModelError(string.Empty, result.IsLockedOut
            ? "الحساب اتقفل مؤقتًا بسبب محاولات كتير غلط. جرب بعد 15 دقيقة"
            : "الكود غلط أو انتهى. اكتب الكود اللي ظاهر في التطبيق دلوقتي");
        return View(model);
    }

    // ---------- Forgot / reset password (by email) ----------

    [AllowAnonymous]
    public IActionResult ForgotPassword()
    {
        ViewData["EmailEnabled"] = _email.IsConfigured;
        return View(new ForgotPasswordViewModel());
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        ViewData["EmailEnabled"] = _email.IsConfigured;
        if (!ModelState.IsValid || !_email.IsConfigured)
            return View(model);

        // The same answer whether or not the email exists, so nobody can probe for accounts
        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user is not null && user.IsActive)
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var link = Url.Action(nameof(ResetPassword), "Account",
                new { email = user.Email, token = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token)) }, Request.Scheme)!;
            try
            {
                await _email.SendAsync(user.Email!, "استرجاع كلمة السر - إمكان HR", $"""
                    <div dir="rtl" style="font-family:Tahoma,Arial,sans-serif;font-size:15px;color:#0f2a4a">
                      <h2 style="font-size:18px">استرجاع كلمة السر</h2>
                      <p>حد طلب تغيير كلمة السر لحسابك. لو إنت، دوس على اللينك ده خلال ساعتين:</p>
                      <p><a href="{WebUtility.HtmlEncode(link)}">اعمل كلمة سر جديدة</a></p>
                      <p style="color:#6a7b8f;font-size:12px">لو مش إنت اللي طلبت، تجاهل الرسالة دي وكلمة السر هتفضل زي ما هي.</p>
                    </div>
                    """);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not send the password reset email");
            }
        }

        ViewData["Sent"] = true;
        return View(model);
    }

    [AllowAnonymous]
    public IActionResult ResetPassword(string? email, string? token)
    {
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(token))
            return RedirectToAction(nameof(Login));
        return View(new ResetPasswordViewModel { Email = email, Token = token });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user is not null)
        {
            string token;
            try { token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(model.Token)); }
            catch (FormatException) { token = ""; }

            var result = await _userManager.ResetPasswordAsync(user, token, model.NewPassword);
            if (result.Succeeded)
            {
                await _userManager.ResetAccessFailedCountAsync(user);
                TempData["Success"] = "كلمة السر اتغيرت. ادخل بالجديدة";
                return RedirectToAction(nameof(Login));
            }
            if (result.Errors.All(e => e.Code != "InvalidToken"))
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);
                return View(model);
            }
        }

        ModelState.AddModelError(string.Empty, "اللينك انتهى أو اتستخدم قبل كده. اطلب لينك جديد");
        return View(model);
    }

    // ---------- My account: password and two-step sign-in ----------

    public async Task<IActionResult> Security()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
            return RedirectToAction(nameof(Login));
        return View(new SecurityViewModel { Email = user.Email!, TwoFactorEnabled = user.TwoFactorEnabled });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword([Bind(Prefix = "ChangePassword")] ChangePasswordViewModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
            return RedirectToAction(nameof(Login));

        if (ModelState.IsValid)
        {
            var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
            if (result.Succeeded)
            {
                // Signs out every other device that used the old password
                await _userManager.UpdateSecurityStampAsync(user);
                await _signInManager.RefreshSignInAsync(user);
                TempData["Success"] = "كلمة السر اتغيرت";
                return RedirectToAction(nameof(Security));
            }
            foreach (var error in result.Errors)
                ModelState.AddModelError("ChangePassword." + (error.Code == "PasswordMismatch" ? nameof(model.CurrentPassword) : nameof(model.NewPassword)),
                    error.Code == "PasswordMismatch" ? "كلمة السر الحالية غلط" : error.Description);
        }

        return View(nameof(Security), new SecurityViewModel { Email = user.Email!, TwoFactorEnabled = user.TwoFactorEnabled, ChangePassword = model });
    }

    public async Task<IActionResult> EnableTwoFactor()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
            return RedirectToAction(nameof(Login));
        if (user.TwoFactorEnabled)
            return RedirectToAction(nameof(Security));

        return View(await SetupModelAsync(user));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EnableTwoFactor(TwoFactorSetupViewModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
            return RedirectToAction(nameof(Login));

        var code = (model.Code ?? "").Replace(" ", "").Replace("-", "");
        var valid = ModelState.IsValid
            && await _userManager.VerifyTwoFactorTokenAsync(user, _userManager.Options.Tokens.AuthenticatorTokenProvider, code);
        if (!valid)
        {
            ModelState.AddModelError(nameof(model.Code), "الكود غلط. اكتب الكود اللي ظاهر في التطبيق دلوقتي");
            return View(await SetupModelAsync(user));
        }

        await _userManager.SetTwoFactorEnabledAsync(user, true);
        var recoveryCodes = await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 8);
        await _signInManager.RefreshSignInAsync(user);
        return View("RecoveryCodes", recoveryCodes!.ToArray());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DisableTwoFactor(string? password)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
            return RedirectToAction(nameof(Login));

        if (string.IsNullOrEmpty(password) || !await _userManager.CheckPasswordAsync(user, password))
        {
            TempData["Error"] = "كلمة السر غلط. التحقق بخطوتين لسه شغال";
            return RedirectToAction(nameof(Security));
        }

        await _userManager.SetTwoFactorEnabledAsync(user, false);
        await _userManager.ResetAuthenticatorKeyAsync(user);
        await _signInManager.RefreshSignInAsync(user);
        TempData["Success"] = "التحقق بخطوتين اتقفل";
        return RedirectToAction(nameof(Security));
    }

    private async Task<TwoFactorSetupViewModel> SetupModelAsync(ApplicationUser user)
    {
        var key = await _userManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrEmpty(key))
        {
            await _userManager.ResetAuthenticatorKeyAsync(user);
            key = await _userManager.GetAuthenticatorKeyAsync(user);
        }

        const string issuer = "Emkan HR";
        var uri = $"otpauth://totp/{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(user.Email!)}?secret={key}&issuer={Uri.EscapeDataString(issuer)}&digits=6";

        // Grouped in fours so it can be typed into the app by hand
        var grouped = string.Join(" ", Enumerable.Range(0, (key!.Length + 3) / 4).Select(i => key.Substring(i * 4, Math.Min(4, key.Length - i * 4))));
        return new TwoFactorSetupViewModel { SharedKey = grouped.ToLowerInvariant(), AuthenticatorUri = uri };
    }
}
