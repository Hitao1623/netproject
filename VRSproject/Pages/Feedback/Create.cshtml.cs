using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using VRSproject.Data;
using VRSproject.Models;
using VRSproject.Services;

namespace VRSproject.Pages.Feedback
{
    [Authorize] // Only logged-in users can give feedback
    public class CreateModel : PageModel
    {
        private readonly FeedbackService _feedbackService;
        private readonly ApplicationDbContext _dbContext; // Needed to fetch rental/vehicle details for display

        public CreateModel(FeedbackService feedbackService, ApplicationDbContext dbContext)
        {
            _feedbackService = feedbackService;
            _dbContext = dbContext;
        }

        // This will hold the data we need to display to the user on the form
        public RentalInfoVM RentalInfo { get; set; } = new RentalInfoVM();

        // This will be bound to the form inputs
        [BindProperty]
        public FeedbackFormVM Form { get; set; } = new FeedbackFormVM();

        public class RentalInfoVM
        {
            public Guid RentalId { get; set; }
            public string VehicleDescription { get; set; } = ""; // e.g., "2023 Honda Civic (Car)"
            public DateTime RentalPeriod { get; set; }
        }

        public class FeedbackFormVM
        {
            [Required]
            public Guid RentalId { get; set; }

            [Required, Range(1, 5, ErrorMessage = "Please select a rating between 1 and 5.")]
            public int Rating { get; set; }

            [Required, StringLength(500, ErrorMessage = "Comment must be less than 500 characters.")]
            public string Comment { get; set; } = "";
        }

        public async Task<IActionResult> OnGetAsync(Guid rentalId)
        {
            // 1. Get the current user's ID
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (userId == null)
            {
                return Challenge(); // Force login if not authenticated
            }

            // 2. Check if the user is eligible to give feedback for this rental
            bool canGiveFeedback = await _feedbackService.CanUserGiveFeedbackAsync(rentalId, userId);
            if (!canGiveFeedback)
            {
                // Rental doesn't exist, doesn't belong to user, isn't completed, or feedback already exists
                TempData["ErrorMessage"] = "You are not eligible to give feedback for this rental. It may not be completed, or you may have already submitted feedback.";
                return RedirectToPage("/Rentals/Index"); // Redirect to their rentals list
            }

            // 3. If eligible, fetch rental details to display to the user
            var rental = await _dbContext.Rentals
                .Include(r => r.Vehicle)
                .FirstOrDefaultAsync(r => r.RentalId == rentalId);

            if (rental == null || rental.Vehicle == null)
            {
                TempData["ErrorMessage"] = "Rental information could not be found.";
                return RedirectToPage("/Rentals/Index");
            }

            // 4. Populate the display ViewModel
            RentalInfo = new RentalInfoVM
            {
                RentalId = rentalId,
                VehicleDescription = $"{rental.Vehicle.Year} {rental.Vehicle.Make} {rental.Vehicle.Model} ({rental.Vehicle.VehicleType})",
                RentalPeriod = rental.StartAtUtc // You could format this better for display
            };

            // 5. Pre-populate the form's RentalId (as a hidden field)
            Form.RentalId = rentalId;

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (userId == null)
            {
                return Challenge();
            }

            // 1. Revalidate eligibility on post to prevent forged requests
            bool canGiveFeedback = await _feedbackService.CanUserGiveFeedbackAsync(Form.RentalId, userId);
            if (!canGiveFeedback)
            {
                TempData["ErrorMessage"] = "You are not eligible to give feedback for this rental. It may not be completed, or you may have already submitted feedback.";
                return RedirectToPage("/Rentals/Index");
            }

            if (!ModelState.IsValid)
            {
                // If the form is invalid, we need to reload the display data
                await LoadRentalInfo(Form.RentalId);
                return Page();
            }

            // 2. Get the rental to link to the feedback and get the VehicleId
            var rental = await _dbContext.Rentals
                .FirstOrDefaultAsync(r => r.RentalId == Form.RentalId);

            if (rental == null)
            {
                TempData["ErrorMessage"] = "Rental information could not be found.";
                return RedirectToPage("/Rentals/Index");
            }

            // 3. Create and save the new Feedback entity
            var newFeedback = new Models.Feedback
            {
                RentalId = Form.RentalId,
                VehicleId = rental.VehicleId, // Set from the rental
                CustomerId = userId, // Set from the logged-in user
                Rating = Form.Rating,
                Comment = Form.Comment,
                CreatedAtUtc = DateTime.UtcNow
                // FeedbackId is generated automatically
            };

            try
            {
                await _feedbackService.CreateAsync(newFeedback);
                TempData["SuccessMessage"] = "Thank you for your feedback!";
                return RedirectToPage("/Rentals/Index"); // Redirect back to their rentals list
            }
            catch (DbUpdateException ex)
            {
                // This might catch the unique constraint violation if something slipped through
                ModelState.AddModelError("", "Could not save feedback. You may have already submitted feedback for this rental.");
                await LoadRentalInfo(Form.RentalId);
                return Page();
            }
        }

        // Helper method to load rental info for display if ModelState is invalid on POST
        private async Task LoadRentalInfo(Guid rentalId)
        {
            var rental = await _dbContext.Rentals
                .Include(r => r.Vehicle)
                .FirstOrDefaultAsync(r => r.RentalId == rentalId);

            if (rental != null && rental.Vehicle != null)
            {
                RentalInfo = new RentalInfoVM
                {
                    RentalId = rentalId,
                    VehicleDescription = $"{rental.Vehicle.Year} {rental.Vehicle.Make} {rental.Vehicle.Model} ({rental.Vehicle.VehicleType})",
                    RentalPeriod = rental.StartAtUtc
                };
            }
        }
    }
}