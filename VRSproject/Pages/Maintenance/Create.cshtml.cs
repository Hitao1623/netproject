using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VRSproject.Data;
using VRSproject.Services;

namespace VRSproject.Pages.Maintenance
{
    [Authorize(Roles = "Admin")]
    public class CreateModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        private readonly MaintenanceService _svc;
        public CreateModel(ApplicationDbContext db, MaintenanceService svc)
        {
            _db = db; _svc = svc;
        }

        public List<(Guid Id, string Label)> VehicleOptions { get; set; } = new();

        public class InputVM
        {
            [Required] public Guid VehicleId { get; set; }

        
            [Required, Display(Name = "Start (local)")]
            public DateTime StartLocal { get; set; } = DateTime.Now;

            [Display(Name = "End (local)")]
            public DateTime? EndLocal { get; set; }

            [StringLength(500)]
            public string? Description { get; set; }
        }

        [BindProperty] public InputVM Form { get; set; } = new();

        public async Task OnGetAsync(Guid? vehicleId)
        {
            await LoadVehicleOptionsAsync();
            if (vehicleId.HasValue) Form.VehicleId = vehicleId.Value;
      
            Form.StartLocal = DateTime.Now.AddMinutes(2);
            Form.EndLocal = DateTime.Now.AddMinutes(10);
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                await LoadVehicleOptionsAsync();
                return Page();
            }

           
            var startUtc = DateTime.SpecifyKind(Form.StartLocal, DateTimeKind.Local).ToUniversalTime();
            DateTime? endUtc = Form.EndLocal.HasValue
                ? DateTime.SpecifyKind(Form.EndLocal.Value, DateTimeKind.Local).ToUniversalTime()
                : null;

            try
            {
                await _svc.CreateAsync(Form.VehicleId, startUtc, endUtc, Form.Description);
                return RedirectToPage("Index", new { vehicleId = Form.VehicleId });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                await LoadVehicleOptionsAsync();
                return Page();
            }
        }

        private async Task LoadVehicleOptionsAsync()
        {
            var cars = await _db.Vehicles
                .OrderBy(v => v.Make).ThenBy(v => v.Model).ThenBy(v => v.Year)
                .Select(v => new { v.VehicleId, v.Make, v.Model, v.Year })
                .ToListAsync();

            VehicleOptions = cars.Select(x => (x.VehicleId, $"{x.Make} {x.Model} ({x.Year})")).ToList();
        }
    }
}
