using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using VRSproject.Models;
using VRSproject.Services;

namespace VRSproject.Pages.Vehicles
{
    public class DetailsModel : PageModel
    {
        private readonly VehicleService _svc;
        public DetailsModel(VehicleService svc) => _svc = svc;

        public Vehicle? Vehicle { get; set; }

        public async Task<IActionResult> OnGetAsync(Guid id)
        {
            Vehicle = await _svc.GetAsync(id);
            return Page();
        }
    }
}
