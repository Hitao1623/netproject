using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VRSproject.Data;
using VRSproject.Services;

namespace VRSproject.Pages.Maintenance
{
    [Authorize(Roles = "Admin")]
    public class FinishModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        private readonly MaintenanceService _svc;
        public FinishModel(ApplicationDbContext db, MaintenanceService svc)
        { _db = db; _svc = svc; }

        public record VM(Guid MaintenanceId, Guid VehicleId, string VehicleLabel,
                         DateTime StartAtUtc, DateTime? EndAtUtc, string? Description);

        [BindProperty] public VM Data { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(Guid id)
        {
            var rec = await _db.MaintenanceRecords.Include(m => m.Vehicle)
                        .FirstOrDefaultAsync(m => m.MaintenanceRecordId == id);
            if (rec == null) return NotFound();

            Data = new VM(rec.MaintenanceRecordId, rec.VehicleId,
                $"{rec.Vehicle?.Make} {rec.Vehicle?.Model} ({rec.Vehicle?.Year})",
                rec.StartAtUtc, rec.EndAtUtc, rec.Description);

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            await _svc.FinishNowAsync(Data.MaintenanceId); 
            TempData["Msg"] = "Maintenance finished.";
            return RedirectToPage("Index", new { vehicleId = Data.VehicleId });
        }
    }
}
