using System.Security.Claims;
using HRSystem.BLL.Services;
using Microsoft.AspNetCore.Mvc;

namespace HRSystem.Web.Controllers;

// The bell in the top bar and the full list; every signed-in user sees only their own
public class NotificationsController : Controller
{
    private const int PageSize = 30;

    private readonly INotificationService _notifications;

    public NotificationsController(INotificationService notifications)
    {
        _notifications = notifications;
    }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    public async Task<IActionResult> Index(int page = 1)
        => View(await _notifications.GetPagedAsync(UserId, page, PageSize));

    // Polled by the bell
    public async Task<IActionResult> Count()
        => Json(new { count = await _notifications.UnreadCountAsync(UserId) });

    // The bell's dropdown, as a fragment
    public async Task<IActionResult> Latest()
        => PartialView("_List", await _notifications.GetLatestAsync(UserId, 8));

    // Marks it read, then goes where it points (only inside the app)
    public async Task<IActionResult> Open(int id)
    {
        var url = await _notifications.OpenAsync(UserId, id);
        return url is not null && Url.IsLocalUrl(url) ? LocalRedirect(url) : RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReadAll(string? returnUrl)
    {
        await _notifications.MarkAllReadAsync(UserId);
        return returnUrl is not null && Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl) : RedirectToAction(nameof(Index));
    }
}
