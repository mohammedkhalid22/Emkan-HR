using HRSystem.BLL.DTOs;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace HRSystem.Web.ViewModels;

public class CandidateIndexViewModel
{
    public CandidateFilterDto Filter { get; set; } = new();
    public List<CandidateListItemDto> Items { get; set; } = [];
    public IEnumerable<SelectListItem> JobTitles { get; set; } = [];

    // Jobs whose CVs can only get an estimated score
    public HashSet<int> JobsWithoutRequirements { get; set; } = [];
    public bool IsAvailable { get; set; }
}
