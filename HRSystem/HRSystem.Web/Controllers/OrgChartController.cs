using HRSystem.BLL.Services;
using Microsoft.AspNetCore.Mvc;

namespace HRSystem.Web.Controllers;

// The org tree: administrations → sections → positions and who holds them (everyone may look)
public class OrgChartController : Controller
{
    private readonly IPositionService _positionService;

    public OrgChartController(IPositionService positionService)
    {
        _positionService = positionService;
    }

    public async Task<IActionResult> Index() => View(await _positionService.GetTreeAsync());
}
