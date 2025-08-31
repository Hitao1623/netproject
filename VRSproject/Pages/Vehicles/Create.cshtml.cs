using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using VRSproject.Models;
using VRSproject.Services;

namespace VRSproject.Pages.Vehicles
{
    [Authorize(Roles = "Admin")]
    public class CreateModel : PageModel
    {
        private readonly VehicleService _svc;
        public CreateModel(VehicleService svc) => _svc = svc;

      
        public class VehicleEditVM
        {
            [Required] public VehicleType VehicleType { get; set; } = VehicleType.Car;

            [Required, StringLength(50)] public string Make { get; set; } = "";
            [Required, StringLength(50)] public string Model { get; set; } = "";
            [Range(1950, 2100)] public int Year { get; set; } = DateTime.UtcNow.Year;

            // Car
            [Range(1, 20)] public int? NumberOfSeats { get; set; }

            // Bike
            [StringLength(40)] public string? BikeType { get; set; }

            // Truck
            [Range(0, 1000000)] public decimal? CargoCapacity { get; set; }
        }

        [BindProperty] public VehicleEditVM Form { get; set; } = new();

        public void OnGet() { }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();

            Vehicle entity = Form.VehicleType switch
            {
                VehicleType.Car => new Car
                {
                    VehicleType = VehicleType.Car,
                    Make = Form.Make,
                    Model = Form.Model,
                    Year = Form.Year,
                    NumberOfSeats = Form.NumberOfSeats ?? 4
                },
                VehicleType.Bike => new Bike
                {
                    VehicleType = VehicleType.Bike,
                    Make = Form.Make,
                    Model = Form.Model,
                    Year = Form.Year,
                    BikeType = Form.BikeType ?? ""
                },
                VehicleType.Truck => new Truck
                {
                    VehicleType = VehicleType.Truck,
                    Make = Form.Make,
                    Model = Form.Model,
                    Year = Form.Year,
                    CargoCapacity = Form.CargoCapacity ?? 0
                },
                _ => throw new ArgumentOutOfRangeException()
            };

            await _svc.CreateAsync(entity);
            return RedirectToPage("Index");
        }
    }
}
