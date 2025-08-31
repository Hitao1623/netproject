using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using VRSproject.Models;
using VRSproject.Services;

namespace VRSproject.Pages.Vehicles
{
    [Authorize(Roles = "Admin")]
    public class EditModel : PageModel
    {
        private readonly VehicleService _svc;
        public EditModel(VehicleService svc) => _svc = svc;

        public class VehicleEditVM
        {
            [Required] public Guid VehicleId { get; set; }
            [Required] public VehicleType VehicleType { get; set; }

            [Required, StringLength(50)] public string Make { get; set; } = "";
            [Required, StringLength(50)] public string Model { get; set; } = "";
            [Range(1950, 2100)] public int Year { get; set; }

            public VehicleStatus Status { get; set; }

            // Car
            [Range(1, 20)] public int? NumberOfSeats { get; set; }
            // Bike
            [StringLength(40)] public string? BikeType { get; set; }
            // Truck
            [Range(0, 1000000)] public decimal? CargoCapacity { get; set; }
        }

        [BindProperty] public VehicleEditVM Form { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(Guid id)
        {
            var v = await _svc.GetAsync(id);
            Form = v switch
            {
                Car car => new VehicleEditVM
                {
                    VehicleId = car.VehicleId,
                    VehicleType = VehicleType.Car,
                    Make = car.Make,
                    Model = car.Model,
                    Year = car.Year,
                    Status = car.Status,
                    NumberOfSeats = car.NumberOfSeats
                },
                Bike bike => new VehicleEditVM
                {
                    VehicleId = bike.VehicleId,
                    VehicleType = VehicleType.Bike,
                    Make = bike.Make,
                    Model = bike.Model,
                    Year = bike.Year,
                    Status = bike.Status,
                    BikeType = bike.BikeType
                },
                Truck truck => new VehicleEditVM
                {
                    VehicleId = truck.VehicleId,
                    VehicleType = VehicleType.Truck,
                    Make = truck.Make,
                    Model = truck.Model,
                    Year = truck.Year,
                    Status = truck.Status,
                    CargoCapacity = truck.CargoCapacity
                },
                _ => throw new InvalidOperationException()
            };
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();

            var v = await _svc.GetAsync(Form.VehicleId);
            v.Make = Form.Make;
            v.Model = Form.Model;
            v.Year = Form.Year;
            v.Status = Form.Status;

            switch (v)
            {
                case Car car:
                    car.NumberOfSeats = Form.NumberOfSeats ?? car.NumberOfSeats;
                    break;
                case Bike bike:
                    bike.BikeType = Form.BikeType ?? bike.BikeType;
                    break;
                case Truck truck:
                    truck.CargoCapacity = Form.CargoCapacity ?? truck.CargoCapacity;
                    break;
            }

            await _svc.UpdateAsync(v);
            return RedirectToPage("Index");
        }
    }
}
