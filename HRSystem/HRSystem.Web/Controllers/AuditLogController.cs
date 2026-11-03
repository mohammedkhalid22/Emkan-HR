using HRSystem.BLL.Services;
using HRSystem.Domain.Identity;
using HRSystem.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRSystem.Web.Controllers;

// Who changed what, and when; read-only
[Authorize(Roles = Roles.Admin)]
public class AuditLogController : Controller
{
    private readonly IAuditLogService _auditLog;

    public AuditLogController(IAuditLogService auditLog)
    {
        _auditLog = auditLog;
    }

    public async Task<IActionResult> Index([FromQuery] AuditLogFilter filter)
        => View(new AuditLogViewModel
        {
            Filter = filter,
            Result = await _auditLog.SearchAsync(filter),
            Entities = await _auditLog.EntityNamesAsync(),
            Actors = await _auditLog.ActorsAsync()
        });
}
