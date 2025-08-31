using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using VRSproject.Services;

namespace VRSproject.Pages.Maintenance
{
    [Authorize(Roles = "Admin")]
    public class DetailsModel : PageModel
    {
        private readonly MaintenanceService _svc;
        public DetailsModel(MaintenanceService svc) => _svc = svc;

        public record VM(
            Guid Id, Guid VehicleId, string VehicleLabel,
            DateTime StartAtUtc, DateTime? EndAtUtc,
            string? Description, string StatusText);

        public VM Data { get; private set; } = default!;

        public async Task<IActionResult> OnGetAsync(Guid id)
        {
            var rec = await _svc.FindAsync(id);
            if (rec == null) return NotFound();

            var stateText = MaintenanceService.GetStateAndText(rec, DateTime.UtcNow, out var text);

            Data = new VM(
                rec.MaintenanceRecordId, rec.VehicleId,
                $"{rec.Vehicle?.Make} {rec.Vehicle?.Model} ({rec.Vehicle?.Year})",
                rec.StartAtUtc, rec.EndAtUtc,
                rec.Description, text);

            return Page();
        }
    }
}
