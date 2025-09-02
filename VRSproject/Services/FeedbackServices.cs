using Microsoft.EntityFrameworkCore;
using VRSproject.Data;
using VRSproject.Models;

namespace VRSproject.Services
{
    public class FeedbackService
    {
        private readonly ApplicationDbContext _db;
        public FeedbackService(ApplicationDbContext db) => _db = db;

        // Crucial: Check if the user can give feedback for this rental
        public async Task<bool> CanUserGiveFeedbackAsync(Guid rentalId, string userId)
        {
            return await _db.Rentals.AnyAsync(r =>
                r.RentalId == rentalId &&
                r.CustomerId == userId &&
                r.Status == RentalStatus.Completed && // Ensure the rental is finished
                !r.Feedbacks.Any() // Check the unique constraint: no feedback exists for this rental yet
            );
        }

        public async Task<Feedback?> GetByIdAsync(Guid feedbackId)
        {
            return await _db.Feedbacks
                .Include(f => f.Vehicle)
                .Include(f => f.Rental)
                .FirstOrDefaultAsync(f => f.FeedbackId == feedbackId);
        }

        public async Task<List<Feedback>> GetForUserAsync(string userId, int page = 1, int pageSize = 20)
        {
            return await _db.Feedbacks
                .Where(f => f.CustomerId == userId)
                .Include(f => f.Vehicle)
                .OrderByDescending(f => f.CreatedAtUtc)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<List<Feedback>> GetForVehicleAsync(Guid vehicleId, int page = 1, int pageSize = 20)
        {
            return await _db.Feedbacks
                .Where(f => f.VehicleId == vehicleId)
                .Include(f => f.Customer) // Assuming you want to show who left the feedback
                .OrderByDescending(f => f.CreatedAtUtc)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task CreateAsync(Feedback feedback)
        {
            // The check for eligibility (CanUserGiveFeedbackAsync) should be done BEFORE calling this method
            _db.Feedbacks.Add(feedback);
            await _db.SaveChangesAsync();
        }

        // Optional: An update method if you allow editing feedback
        public async Task UpdateAsync(Feedback feedback)
        {
            feedback.UpdatedAtUtc = DateTime.UtcNow;
            _db.Feedbacks.Update(feedback);
            await _db.SaveChangesAsync();
        }
    }
}