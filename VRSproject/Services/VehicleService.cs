using Microsoft.EntityFrameworkCore;
using VRSproject.Data;
using VRSproject.Models;

namespace VRSproject.Services
{
    public class VehicleService
    {
        private readonly ApplicationDbContext _db;
        public VehicleService(ApplicationDbContext db) => _db = db;

        public async Task<(IReadOnlyList<Vehicle> Items, int Total)> SearchAsync(
            VehicleType? type, string? make, string? model, int page = 1, int pageSize = 20)
        {
            var q = _db.Vehicles.AsQueryable();

            if (type is not null) q = q.Where(v => v.VehicleType == type);
            if (!string.IsNullOrWhiteSpace(make)) q = q.Where(v => v.Make.Contains(make));
            if (!string.IsNullOrWhiteSpace(model)) q = q.Where(v => v.Model.Contains(model));

            var total = await q.CountAsync();
            var items = await q.OrderBy(v => v.VehicleType).ThenBy(v => v.Make).ThenBy(v => v.Model)
                               .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
            return (items, total);
        }

        public Task<Vehicle> GetAsync(Guid id) =>
            _db.Vehicles.FirstAsync(v => v.VehicleId == id);

        public async Task CreateAsync(Vehicle v)
        {
            _db.Vehicles.Add(v);
            await _db.SaveChangesAsync();
        }

        public async Task UpdateAsync(Vehicle v)
        {
            _db.Vehicles.Update(v);
            await _db.SaveChangesAsync();
        }

        /// only allow delete if no related reservations/rentals/maintenance/feedback

        public async Task DeleteAsync(Guid vehicleId)
        {
            var hasDeps =
                await _db.Reservations.AnyAsync(r => r.VehicleId == vehicleId) ||
                await _db.Rentals.AnyAsync(r => r.VehicleId == vehicleId) ||
                await _db.MaintenanceRecords.AnyAsync(m => m.VehicleId == vehicleId) ||
                await _db.Feedbacks.AnyAsync(f => f.VehicleId == vehicleId);

            if (hasDeps)
                throw new InvalidOperationException("Cannot delete vehicle with related reservations/rentals/maintenance/feedback.");

            var v = await _db.Vehicles.FirstOrDefaultAsync(x => x.VehicleId == vehicleId);
            if (v == null) return;

            _db.Vehicles.Remove(v);
            await _db.SaveChangesAsync();
        }
    }
}
