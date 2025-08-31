using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using VRSproject.Models;
using VRSproject.Services;

namespace VRSproject.Pages.Vehicles
{
    public class IndexModel : PageModel
    {
        private readonly VehicleService _svc;
        public IndexModel(VehicleService svc) => _svc = svc;

        [BindProperty(SupportsGet = true)] public VehicleType? Type { get; set; }
        [BindProperty(SupportsGet = true)] public string? Make { get; set; }
        [BindProperty(SupportsGet = true)] public string? ModelFilter { get; set; }

        [BindProperty(SupportsGet = true)] public int PageNumber { get; set; } = 1;

        public IReadOnlyList<Vehicle> Vehicles { get; set; } = Array.Empty<Vehicle>();
        public int Total { get; set; }
        public int PageSize { get; } = 20;

        public async Task OnGetAsync()
        {
            var (items, total) = await _svc.SearchAsync(Type, Make, ModelFilter, PageNumber, PageSize);
            Vehicles = items;
            Total = total;
        }
    }
}
