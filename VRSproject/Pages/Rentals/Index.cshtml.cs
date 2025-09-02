using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VRSproject.Data;
using VRSproject.Models;

namespace VRSproject.Pages.Rentals
{
    [Authorize] // User must be logged in to view their rentals
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public IndexModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public List<RentalViewModel> Rentals { get; set; } = new List<RentalViewModel>();

        public async Task OnGetAsync()
        {
            // Get the current user's ID
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (userId == null) return;

            // Query the user's rentals with related data
            var userRentals = await _context.Rentals
                .Where(r => r.CustomerId == userId)
                .Include(r => r.Vehicle)
                .OrderByDescending(r => r.StartAtUtc)
                .ToListAsync();

            // Convert to ViewModel for display
            Rentals = userRentals.Select(r => new RentalViewModel
            {
                RentalId = r.RentalId,
                VehicleDescription = $"{r.Vehicle?.Year} {r.Vehicle?.Make} {r.Vehicle?.Model}",
                VehicleType = r.Vehicle?.VehicleType ?? VehicleType.Car,
                StartDate = r.StartAtUtc,
                EndDate = r.EndAtUtc,
                Status = r.Status,
                TotalCost = r.TotalCost,
                CanGiveFeedback = r.Status == RentalStatus.Completed &&
                                !_context.Feedbacks.Any(f => f.RentalId == r.RentalId)
            }).ToList();
        }

        public class RentalViewModel
        {
            public Guid RentalId { get; set; }
            public string VehicleDescription { get; set; } = "";
            public VehicleType VehicleType { get; set; }
            public DateTime StartDate { get; set; }
            public DateTime? EndDate { get; set; }
            public RentalStatus Status { get; set; }
            public decimal TotalCost { get; set; }
            public bool CanGiveFeedback { get; set; }
        }
    }
}