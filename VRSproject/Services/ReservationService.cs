using Microsoft.EntityFrameworkCore;
using VRSproject.Data;
using VRSproject.Models;

namespace VRSproject.Services
{
    public class ReservationService
    {
        private readonly ApplicationDbContext _db;
        private readonly IServiceProvider _sp; // to optionally resolve IPricingService

        public ReservationService(ApplicationDbContext db, IServiceProvider sp)
        {
            _db = db;
            _sp = sp;
        }

        // ---------- UTC helpers ----------
        private static DateTime AsUtc(DateTime dt) =>
            dt.Kind == DateTimeKind.Utc ? dt
              : dt.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(dt, DateTimeKind.Utc)
              : dt.ToUniversalTime();

        // ---------- Public API ----------
        public async Task<Reservation> GetAsync(Guid id) =>
            await _db.Reservations
                .Include(r => r.Vehicle)
                .Include(r => r.Customer)
                .FirstAsync(r => r.ReservationId == id);

        public async Task<List<Reservation>> ListForUserAsync(string userId, int page = 1, int pageSize = 20) =>
            await _db.Reservations.Where(r => r.CustomerId == userId)
                .Include(r => r.Vehicle)
                .OrderByDescending(r => r.CreatedAtUtc)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .ToListAsync();

        public async Task<decimal> QuoteAsync(Guid vehicleId, DateTime fromUtc, DateTime toUtc, PricingType pt)
        {
            fromUtc = AsUtc(fromUtc); toUtc = AsUtc(toUtc);
            if (toUtc <= fromUtc) throw new ArgumentException("End must be after start.");

            // Prefer team Dev 2's pricing if registered
            var pricing = _sp.GetService(typeof(IPricingService)) as IPricingService;
            if (pricing is not null)
                return await pricing.QuoteAsync(vehicleId, fromUtc, toUtc, pt);

            // Fallback (base rate * multiplier * units)
            return await QuoteFallbackAsync(vehicleId, fromUtc, toUtc, pt);
        }

        public async Task<Reservation> CreateAsync(Guid vehicleId, string customerId, DateTime fromUtc, DateTime toUtc, PricingType pt)
        {
            fromUtc = AsUtc(fromUtc); toUtc = AsUtc(toUtc);
            if (toUtc <= fromUtc) throw new ArgumentException("End must be after start.");

            var vehicle = await _db.Vehicles.FirstOrDefaultAsync(v => v.VehicleId == vehicleId)
                          ?? throw new InvalidOperationException("Vehicle not found.");

            // Prevent overlap with existing bookings and maintenance
            if (await HasConflictAsync(vehicleId, fromUtc, toUtc))
                throw new InvalidOperationException("Selected time conflicts with another reservation, rental, or maintenance window.");

            var quote = await QuoteAsync(vehicleId, fromUtc, toUtc, pt);

            var r = new Reservation
            {
                ReservationId = Guid.NewGuid(),
                VehicleId = vehicleId,
                CustomerId = customerId,
                ReservedFromUtc = fromUtc,
                ReservedToUtc = toUtc,
                Status = ReservationStatus.Confirmed, // you can switch to Pending if you want a separate confirm step
                QuoteAmount = quote
            };

            _db.Reservations.Add(r);
            await _db.SaveChangesAsync();
            return r;
        }

        public async Task ConfirmAsync(Guid reservationId, string userId, bool isAdmin = false)
        {
            var r = await _db.Reservations.FirstOrDefaultAsync(x => x.ReservationId == reservationId)
                    ?? throw new InvalidOperationException("Reservation not found.");
            if (!isAdmin && r.CustomerId != userId) throw new UnauthorizedAccessException();

            if (r.Status == ReservationStatus.Cancelled) throw new InvalidOperationException("Cancelled reservation cannot be confirmed.");
            r.Status = ReservationStatus.Confirmed;
            _db.Reservations.Update(r);
            await _db.SaveChangesAsync();
        }

        public async Task CancelAsync(Guid reservationId, string userId, bool isAdmin = false)
        {
            var r = await _db.Reservations.FirstOrDefaultAsync(x => x.ReservationId == reservationId)
                    ?? throw new InvalidOperationException("Reservation not found.");
            if (!isAdmin && r.CustomerId != userId) throw new UnauthorizedAccessException();

            r.Status = ReservationStatus.Cancelled;
            _db.Reservations.Update(r);
            await _db.SaveChangesAsync();
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

            // Most specific plan first (per-vehicle), then type-level
            var plan = await _db.PricingPlans
                .Where(p =>
                    p.PricingType == pt &&
                    p.IsActive &&
                    (p.EffectiveFromUtc == null || p.EffectiveFromUtc <= now) &&
                    (p.EffectiveToUtc == null || now < p.EffectiveToUtc) &&
                    (p.VehicleId == vehicleId || p.VehicleType == vehicle.VehicleType))
                .OrderByDescending(p => p.VehicleId != null) // prefer per-vehicle plan
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
