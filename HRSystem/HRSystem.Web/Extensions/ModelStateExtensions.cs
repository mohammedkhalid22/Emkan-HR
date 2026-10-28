using HRSystem.BLL.Common;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace HRSystem.Web.Extensions;

public static class ModelStateExtensions
{
    // Shows a business-rule error from a service under its field (or at the top of the form)
    public static void AddServiceError(this ModelStateDictionary modelState, ServiceResult result)
        => modelState.AddModelError(result.Field ?? string.Empty, result.Error ?? "حصل خطأ");
}
