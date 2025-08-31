using System.ComponentModel.DataAnnotations;
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
    public class EditModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        private readonly MaintenanceService _svc;

        public EditModel(ApplicationDbContext db, MaintenanceService svc)
        {
            _db = db;
            _svc = svc;
        }


        public string Header { get; set; } = "Edit Maintenance";

        public record VM(Guid Id, Guid VehicleId, string VehicleLabel);
        public VM Data { get; set; } = default!;

        public class InputVM
        {
            [Required, Display(Name = "Start (UTC)")]
            public DateTime StartAtUtc { get; set; }

            [Display(Name = "End (UTC)")]
            public DateTime? EndAtUtc { get; set; }

            [StringLength(500)]
            public string? Description { get; set; }
        }

        [BindProperty]
        public InputVM Form { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(Guid id)
        {
            var rec = await _db.MaintenanceRecords
                               .Include(m => m.Vehicle)
                               .FirstOrDefaultAsync(m => m.MaintenanceRecordId == id);
            if (rec == null) return NotFound();

            Data = new VM(rec.MaintenanceRecordId,
                          rec.VehicleId,
                          $"{rec.Vehicle?.Make} {rec.Vehicle?.Model} ({rec.Vehicle?.Year})");

        
            Form = new InputVM
            {
                StartAtUtc = DateTime.SpecifyKind(rec.StartAtUtc, DateTimeKind.Utc),
                EndAtUtc = rec.EndAtUtc.HasValue
                    ? DateTime.SpecifyKind(rec.EndAtUtc.Value, DateTimeKind.Utc)
                    : null,
                Description = rec.Description
            };
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(Guid id)
        {
            if (!ModelState.IsValid) return Page();

          
            var startUtc = DateTime.SpecifyKind(Form.StartAtUtc, DateTimeKind.Utc);
            DateTime? endUtc = Form.EndAtUtc.HasValue
                ? DateTime.SpecifyKind(Form.EndAtUtc.Value, DateTimeKind.Utc)
                : null;

            try
            {
                await _svc.UpdateAsync(id, startUtc, endUtc, Form.Description);
                TempData["Msg"] = "Maintenance updated.";
                return RedirectToPage("Index");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                return Page();
            }
        }
    }
}
