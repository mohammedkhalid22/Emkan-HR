using HRSystem.BLL.Services;
using HRSystem.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRSystem.Web.Controllers;

[Authorize(Roles = Roles.AdminOrHR)]
public class StatisticsController : Controller
{
    private readonly IStatisticsService _statisticsService;

    public StatisticsController(IStatisticsService statisticsService)
    {
        _statisticsService = statisticsService;
    }

    public async Task<IActionResult> Index(int? year)
    {
        var y = year is >= 2000 and <= 2100 ? year.Value : DateTime.Today.Year;
        return View(await _statisticsService.GetAsync(y));
    }
}
