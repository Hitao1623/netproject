using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VRSproject.Data;
using VRSproject.Models;
using VRSproject.Services;

namespace VRSproject.Pages.Maintenance
{
    [Authorize(Roles = "Admin")]
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        private readonly MaintenanceService _svc;
        public IndexModel(ApplicationDbContext db, MaintenanceService svc)
        { _db = db; _svc = svc; }

        [BindProperty(SupportsGet = true)] public Guid? VehicleId { get; set; }
        public List<(Guid Id, string Label)> VehicleOptions { get; set; } = new();
        public List<MaintenanceRecord> Records { get; set; } = new();
        public DateTime NowUtc { get; private set; } = DateTime.UtcNow;

        public async Task OnGetAsync()
        {
            var cars = await _db.Vehicles
                .OrderBy(v => v.Make).ThenBy(v => v.Model).ThenBy(v => v.Year)
                .Select(v => new { v.VehicleId, v.Make, v.Model, v.Year })
                .ToListAsync();

            VehicleOptions = cars.Select(x => (x.VehicleId, $"{x.Make} {x.Model} ({x.Year})")).ToList();

            Records = await _svc.ListAsync(VehicleId);
            NowUtc = DateTime.UtcNow;

        }

        public async Task<IActionResult> OnPostCancelAsync(Guid id, Guid? vehicleId)
        {
            try
            {
                await _svc.CancelAsync(id);
                TempData["Msg"] = "Maintenance cancelled.";
            }
            catch (Exception ex)
            {
                TempData["Msg"] = ex.Message;
            }
            return RedirectToPage("Index", new { vehicleId });
        }
    }
}
