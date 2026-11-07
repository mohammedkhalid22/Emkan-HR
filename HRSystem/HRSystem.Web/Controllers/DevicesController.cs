using HRSystem.BLL.Devices;
using HRSystem.BLL.Services;
using HRSystem.Domain.Identity;
using HRSystem.Web.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRSystem.Web.Controllers;

// Fingerprint devices (ZKTeco over IP)
[Authorize(Roles = Roles.AdminOrHR)]
public class DevicesController : Controller
{
    private readonly IDeviceService _devices;

    public DevicesController(IDeviceService devices)
    {
        _devices = devices;
    }

    public async Task<IActionResult> Index() => View(await _devices.GetAllAsync());

    public IActionResult Create() => View(new SaveDeviceDto());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(SaveDeviceDto model)
    {
        if (ModelState.IsValid)
        {
            var result = await _devices.CreateAsync(model);
            if (result.Succeeded)
            {
                TempData["Success"] = "الجهاز اتضاف";
                return RedirectToAction(nameof(Index));
            }
            ModelState.AddServiceError(result);
        }
        return View(model);
    }

    public async Task<IActionResult> Edit(int id)
    {
        var dto = await _devices.GetForEditAsync(id);
        return dto is null ? NotFound() : View(dto);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, SaveDeviceDto model)
    {
        if (id != model.Id)
            return BadRequest();
        if (ModelState.IsValid)
        {
            var result = await _devices.UpdateAsync(model);
            if (result.Succeeded)
            {
                TempData["Success"] = "التعديلات اتحفظت";
                return RedirectToAction(nameof(Index));
            }
            ModelState.AddServiceError(result);
        }
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _devices.DeleteAsync(id);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? "الجهاز اتمسح" : result.Error;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Pull(int id)
    {
        var result = await _devices.PullAsync(id, HttpContext.RequestAborted);
        if (result.Succeeded)
        {
            var r = result.Value!;
            TempData["Success"] = $"اتقرا {r.Received} بصمة من الجهاز، منهم {r.New} جديدة، واتحدّث حضور {r.DaysUpdated} يوم";
            if (r.Problems.Count > 0)
                TempData["Error"] = string.Join(" · ", r.Problems);
        }
        else
        {
            TempData["Error"] = result.Error;
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> NewKey(int id)
    {
        var result = await _devices.NewApiKeyAsync(id);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded
            ? "اتعمل مفتاح جديد. حدّثه في إعدادات البرنامج اللي على جهاز الشركة"
            : result.Error;
        return RedirectToAction(nameof(Index));
    }
}

// Where the company-side agent sends punches: POST api/devices/punches, header X-Device-Key
[ApiController]
[AllowAnonymous]
[IgnoreAntiforgeryToken]
[Route("api/devices")]
public class DevicePunchesApiController : ControllerBase
{
    public record PunchIn(string UserId, DateTime Time);
    public record PunchBatch(List<PunchIn> Punches);

    private readonly IDeviceService _devices;

    public DevicePunchesApiController(IDeviceService devices)
    {
        _devices = devices;
    }

    [HttpPost("punches")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<IActionResult> Punches([FromHeader(Name = "X-Device-Key")] string? key, [FromBody] PunchBatch batch)
    {
        if (string.IsNullOrWhiteSpace(key))
            return Unauthorized(new { error = "X-Device-Key مطلوب" });

        var result = await _devices.ReceiveAsync(key.Trim(), batch.Punches.Select(p => new DevicePunch(p.UserId, p.Time)).ToList());
        if (!result.Succeeded)
            return Unauthorized(new { error = result.Error });

        var r = result.Value!;
        return Ok(new { received = r.Received, @new = r.New, daysUpdated = r.DaysUpdated, unmatched = r.Unmatched, problems = r.Problems });
    }
}
