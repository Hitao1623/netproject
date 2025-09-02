using Microsoft.EntityFrameworkCore;
using VRSproject.Data;
using VRSproject.Models;

namespace VRSproject.Services
{
    public class RentalService
    {
        private readonly ApplicationDbContext _db;
        private readonly IServiceProvider _sp;
        private readonly MaintenanceService _maintenance; // reuse NormalizeVehicleStatus

        public RentalService(ApplicationDbContext db, IServiceProvider sp, MaintenanceService maintenance)
        {
            _db = db;
            _sp = sp;
            _maintenance = maintenance;
        }

        private static DateTime AsUtc(DateTime dt) =>
            dt.Kind == DateTimeKind.Utc ? dt
              : dt.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(dt, DateTimeKind.Utc)
              : dt.ToUniversalTime();

        public async Task<Rental> GetAsync(Guid id) =>
            await _db.Rentals
                .Include(r => r.Vehicle).Include(r => r.Customer).Include(r => r.Reservation)
                .FirstAsync(r => r.RentalId == id);

        public async Task<List<Rental>> ListForUserAsync(string userId, int page = 1, int pageSize = 20) =>
            await _db.Rentals.Where(r => r.CustomerId == userId)
                .Include(r => r.Vehicle)
                .OrderByDescending(r => r.StartAtUtc)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .ToListAsync();

        // Start rental from a CONFIRMED reservation
        public async Task<Rental> StartFromReservationAsync(Guid reservationId, string userId)
        {
            var res = await _db.Reservations.Include(r => r.Vehicle).FirstOrDefaultAsync(r => r.ReservationId == reservationId)
                      ?? throw new InvalidOperationException("Reservation not found.");

            if (res.CustomerId != userId) throw new UnauthorizedAccessException();
            if (res.Status != ReservationStatus.Confirmed) throw new InvalidOperationException("Reservation must be confirmed.");

            var now = DateTime.UtcNow;
            var start = now < res.ReservedFromUtc ? res.ReservedFromUtc : now; // pickup at or after fromUtc
            var end = res.ReservedToUtc;

            if (await HasConflictAsync(res.VehicleId, start, end))
                throw new InvalidOperationException("Time conflicts with other bookings/maintenance.");

            var rental = new Rental
            {
                RentalId = Guid.NewGuid(),
                VehicleId = res.VehicleId,
                CustomerId = res.CustomerId,
                ReservationId = res.ReservationId,
                StartAtUtc = AsUtc(start),
                EndAtUtc = null,
                PricingType = InferPricingTypeFromQuote(res.QuoteAmount, res.ReservedFromUtc, res.ReservedToUtc), // heuristic if needed
                TotalCost = 0m,
                Status = RentalStatus.Active
            };

            _db.Rentals.Add(rental);
            await _db.SaveChangesAsync();

            await _maintenance.NormalizeVehicleStatusAsync(res.VehicleId); // mark as Rented if applicable
            return rental;
        }

        // Start a walk-in rental immediately
        public async Task<Rental> StartWalkInAsync(Guid vehicleId, string userId, DateTime startUtc, DateTime plannedEndUtc, PricingType pt)
        {
            startUtc = AsUtc(startUtc); plannedEndUtc = AsUtc(plannedEndUtc);
            if (plannedEndUtc <= startUtc) throw new ArgumentException("End must be after start.");

            if (await HasConflictAsync(vehicleId, startUtc, plannedEndUtc))
                throw new InvalidOperationException("Time conflicts with other bookings/maintenance.");

            var rental = new Rental
            {
                RentalId = Guid.NewGuid(),
                VehicleId = vehicleId,
                CustomerId = userId,
                StartAtUtc = startUtc,
                PricingType = pt,
                TotalCost = 0m,
                Status = RentalStatus.Active
            };

            _db.Rentals.Add(rental);
            await _db.SaveChangesAsync();

            await _maintenance.NormalizeVehicleStatusAsync(vehicleId);
            return rental;
        }

        // Return (complete) a rental and compute final total
        public async Task CompleteAsync(Guid rentalId)
        {
            var rental = await _db.Rentals.Include(r => r.Vehicle).FirstOrDefaultAsync(r => r.RentalId == rentalId)
                         ?? throw new InvalidOperationException("Rental not found.");
            if (rental.Status != RentalStatus.Active) throw new InvalidOperationException("Only active rentals can be completed.");

            rental.EndAtUtc = DateTime.UtcNow;

            var pricing = _sp.GetService(typeof(IPricingService)) as IPricingService;
            if (pricing is null)
            {
                // Safe fallback via ReservationService's fallback logic: re-use a tiny helper here
                rental.TotalCost = await QuoteFallbackAsync(rental.VehicleId, rental.StartAtUtc, rental.EndAtUtc.Value, rental.PricingType);
            }
            else
            {
                rental.TotalCost = await pricing.QuoteAsync(rental.VehicleId, rental.StartAtUtc, rental.EndAtUtc.Value, rental.PricingType);
            }

            rental.Status = RentalStatus.Completed;
            _db.Rentals.Update(rental);
            await _db.SaveChangesAsync();

            await _maintenance.NormalizeVehicleStatusAsync(rental.VehicleId);
        }

        public async Task CancelAsync(Guid rentalId) // optional: for admin/edge cases
        {
            var rental = await _db.Rentals.FirstOrDefaultAsync(r => r.RentalId == rentalId)
                         ?? throw new InvalidOperationException("Rental not found.");
            rental.Status = RentalStatus.Cancelled;
            _db.Rentals.Update(rental);
            await _db.SaveChangesAsync();

            await _maintenance.NormalizeVehicleStatusAsync(rental.VehicleId);
        }

        // ---------- Internal ----------
        private async Task<bool> HasConflictAsync(Guid vehicleId, DateTime s, DateTime e)
        {
            return
                await _db.Reservations.AnyAsync(r =>
                    r.VehicleId == vehicleId &&
                    r.Status != ReservationStatus.Cancelled &&
                    r.ReservedToUtc > s && e > r.ReservedFromUtc)
             || await _db.Rentals.AnyAsync(r =>
                    r.VehicleId == vehicleId &&
                    (r.EndAtUtc ?? DateTime.MaxValue) > s &&
                    r.StartAtUtc < e)
             || await _db.MaintenanceRecords.AnyAsync(m =>
                    m.VehicleId == vehicleId &&
                    (m.EndAtUtc ?? DateTime.MaxValue) > s &&
                    e > m.StartAtUtc);
        }

        private PricingType InferPricingTypeFromQuote(decimal quote, DateTime fromUtc, DateTime toUtc)
        {
            // If you store pricing type on reservation in the future, use that instead.
            var hours = (toUtc - fromUtc).TotalHours;
            if (hours <= 8) return PricingType.Hourly;
            if (hours <= 24 * 7) return PricingType.Daily;
            return PricingType.Weekly;
        }

        private async Task<decimal> QuoteFallbackAsync(Guid vehicleId, DateTime fromUtc, DateTime toUtc, PricingType pt)
        {
            var vehicle = await _db.Vehicles.FirstAsync(v => v.VehicleId == vehicleId);
            var now = DateTime.UtcNow;

            var baseRate = await _db.TypeBaseRates
                .Where(x => x.VehicleType == vehicle.VehicleType
                         && x.PricingType == pt
                         && x.IsActive
                         && (x.EffectiveFromUtc == null || x.EffectiveFromUtc <= now)
                         && (x.EffectiveToUtc == null || now < x.EffectiveToUtc))
                .OrderByDescending(x => x.EffectiveFromUtc)
                .Select(x => x.BaseRate)
                .FirstOrDefaultAsync();

            if (baseRate <= 0) throw new InvalidOperationException("No active base rate configured.");

            var plan = await _db.PricingPlans
                .Where(p =>
                    p.PricingType == pt &&
                    p.IsActive &&
                    (p.EffectiveFromUtc == null || p.EffectiveFromUtc <= now) &&
                    (p.EffectiveToUtc == null || now < p.EffectiveToUtc) &&
                    (p.VehicleId == vehicleId || p.VehicleType == vehicle.VehicleType))
                .OrderByDescending(p => p.VehicleId != null)
                .ThenByDescending(p => p.EffectiveFromUtc)
                .FirstOrDefaultAsync();

            var multiplier = plan?.RateMultiplier > 0 ? plan.RateMultiplier : 1m;

            decimal units = pt switch
            {
                PricingType.Hourly => Math.Ceiling((decimal)(toUtc - fromUtc).TotalHours),
                PricingType.Daily => Math.Ceiling((decimal)(toUtc - fromUtc).TotalDays),
                PricingType.Weekly => Math.Ceiling((decimal)(toUtc - fromUtc).TotalDays / 7m),
                _ => throw new ArgumentOutOfRangeException(nameof(pt))
            };

            return units * baseRate * multiplier;
        }
    }
}
