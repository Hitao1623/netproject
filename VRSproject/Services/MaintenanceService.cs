using Microsoft.EntityFrameworkCore;
using VRSproject.Data;
using VRSproject.Models;

namespace VRSproject.Services
{
    public enum MaintenanceState { Scheduled, InProgress, Completed }

    public class MaintenanceService
    {
        private readonly ApplicationDbContext _db;
        public MaintenanceService(ApplicationDbContext db) => _db = db;

        // ---------- 小工具：确保时间是 UTC ----------
        private static DateTime AsUtc(DateTime dt) =>
            dt.Kind == DateTimeKind.Utc ? dt
                : dt.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(dt, DateTimeKind.Utc)
                : dt.ToUniversalTime();

        private static DateTime? AsUtc(DateTime? dt) => dt.HasValue ? AsUtc(dt.Value) : null;

        // ------- 创建 -------
        public Task CreateAsync(MaintenanceRecord rec)
            => CreateAsync(rec.VehicleId, rec.StartAtUtc, rec.EndAtUtc, rec.Description);

        public async Task CreateAsync(Guid vehicleId, DateTime startUtc, DateTime? endUtc, string? desc)
        {
            // 强制按 UTC 处理
            startUtc = AsUtc(startUtc);
            endUtc = AsUtc(endUtc);

            if (startUtc < DateTime.UtcNow.AddYears(-1)) throw new ArgumentException("Invalid start.");
            if (endUtc is not null && endUtc <= startUtc) throw new ArgumentException("End must be after start.");

            if (await HasConflictAsync(vehicleId, startUtc, endUtc))
                throw new InvalidOperationException("Time conflicts with existing reservation/rental/maintenance.");

            var rec = new MaintenanceRecord
            {
                MaintenanceRecordId = Guid.NewGuid(),
                VehicleId = vehicleId,
                StartAtUtc = startUtc,
                EndAtUtc = endUtc,
                Description = desc ?? ""
            };
            _db.MaintenanceRecords.Add(rec);

            // 若当前就在维护区间内，直接把车辆置为 UnderMaintenance
            var now = DateTime.UtcNow;
            if (startUtc <= now && (!endUtc.HasValue || now < endUtc.Value))
            {
                var v = await _db.Vehicles.FirstAsync(x => x.VehicleId == vehicleId);
                v.Status = VehicleStatus.UnderMaintenance;
            }
            await _db.SaveChangesAsync();
        }

        // ------- 查询 -------
        public Task<List<MaintenanceRecord>> ListAsync(Guid? vehicleId = null)
        {
            var q = _db.MaintenanceRecords.Include(m => m.Vehicle).AsQueryable();
            if (vehicleId is Guid vid) q = q.Where(m => m.VehicleId == vid);
            return q.OrderByDescending(m => m.StartAtUtc).ToListAsync();
        }

        public Task<MaintenanceRecord?> FindAsync(Guid id)
            => _db.MaintenanceRecords.Include(x => x.Vehicle)
                .FirstOrDefaultAsync(x => x.MaintenanceRecordId == id);

        // ------- 编辑 / 取消 / 完成 -------
        public async Task UpdateAsync(Guid id, DateTime startUtc, DateTime? endUtc, string? desc)
        {
            // 强制按 UTC 处理
            startUtc = AsUtc(startUtc);
            endUtc = AsUtc(endUtc);

            if (endUtc is not null && endUtc <= startUtc)
                throw new ArgumentException("End must be after start.");

            var rec = await _db.MaintenanceRecords.FirstOrDefaultAsync(x => x.MaintenanceRecordId == id)
                      ?? throw new InvalidOperationException("Maintenance record not found.");

            // 与“除自身以外”的记录做冲突校验
            if (await HasConflictAsync(rec.VehicleId, startUtc, endUtc, exceptId: id))
                throw new InvalidOperationException("Time conflicts with other records.");

            rec.StartAtUtc = startUtc;
            rec.EndAtUtc = endUtc;
            rec.Description = desc ?? string.Empty;

            _db.MaintenanceRecords.Update(rec);
            await _db.SaveChangesAsync();
            await NormalizeVehicleStatusAsync(rec.VehicleId);
        }

        // 仅允许未开始的记录取消
        public async Task CancelAsync(Guid id)
        {
            var rec = await _db.MaintenanceRecords.FirstOrDefaultAsync(x => x.MaintenanceRecordId == id)
                      ?? throw new InvalidOperationException("Maintenance record not found.");

            if (DateTime.UtcNow >= rec.StartAtUtc)
                throw new InvalidOperationException("Only scheduled (not started) maintenance can be cancelled.");

            var vehicleId = rec.VehicleId;
            _db.MaintenanceRecords.Remove(rec);
            await _db.SaveChangesAsync();
            await NormalizeVehicleStatusAsync(vehicleId);
        }

        // Finish（手动结束，视为“提前完成”）
        public Task FinishNowAsync(Guid maintenanceId) => FinishNowAsync(maintenanceId, markFinishedEarly: true);

        public async Task FinishNowAsync(Guid maintenanceId, bool markFinishedEarly)
        {
            var rec = await _db.MaintenanceRecords
                .FirstOrDefaultAsync(x => x.MaintenanceRecordId == maintenanceId)
                ?? throw new InvalidOperationException("Maintenance record not found.");

            var now = DateTime.UtcNow;
            if (!rec.EndAtUtc.HasValue || rec.EndAtUtc.Value > now)
                rec.EndAtUtc = now;

            if (markFinishedEarly)
            {
                var stamp = $"[Finished early at {now:yyyy-MM-dd HH:mm:ss} UTC]";
                rec.Description = string.IsNullOrWhiteSpace(rec.Description)
                    ? stamp
                    : $"{rec.Description} {stamp}";
            }

            _db.MaintenanceRecords.Update(rec);
            await _db.SaveChangesAsync();
            await NormalizeVehicleStatusAsync(rec.VehicleId);
        }

        // ------- 车辆状态规范化 -------
        public async Task<VehicleStatus> NormalizeVehicleStatusAsync(Guid vehicleId)
        {
            var now = DateTime.UtcNow;

            var inMaintenance = await _db.MaintenanceRecords.AnyAsync(m =>
                m.VehicleId == vehicleId &&
                m.StartAtUtc <= now &&
                (!m.EndAtUtc.HasValue || now < m.EndAtUtc.Value));

            if (inMaintenance)
            {
                await SetVehicleStatusAsync(vehicleId, VehicleStatus.UnderMaintenance);
                return VehicleStatus.UnderMaintenance;
            }

            var inRental = await _db.Rentals.AnyAsync(r =>
                r.VehicleId == vehicleId &&
                r.Status == RentalStatus.Active &&
                r.StartAtUtc <= now &&
                (!r.EndAtUtc.HasValue || now < r.EndAtUtc.Value));

            if (inRental)
            {
                await SetVehicleStatusAsync(vehicleId, VehicleStatus.Rented);
                return VehicleStatus.Rented;
            }

            await SetVehicleStatusAsync(vehicleId, VehicleStatus.Available);
            return VehicleStatus.Available;
        }

        // ------- 状态判定（页面用它来决定按钮） -------
        public static MaintenanceState GetStateAndText(MaintenanceRecord r, DateTime nowUtc, out string text)
        {
            if (nowUtc < r.StartAtUtc)
            {
                text = "scheduled";
                return MaintenanceState.Scheduled;
            }

            if (!r.EndAtUtc.HasValue || nowUtc < r.EndAtUtc.Value)
            {
                text = "in progress";
                return MaintenanceState.InProgress;
            }

            // completed
            text = (r.Description ?? "").Contains("[Finished early", StringComparison.OrdinalIgnoreCase)
                ? $"completed in advance at {r.EndAtUtc:yyyy-MM-dd HH:mm:ss} UTC"
                : "completed";
            return MaintenanceState.Completed;
        }

        // ------- 内部辅助 -------
        // 通用冲突（可选排除某条记录）
        private async Task<bool> HasConflictAsync(Guid vehicleId, DateTime startUtc, DateTime? endUtc, Guid? exceptId = null)
        {
            var s = startUtc;
            var e = endUtc ?? DateTime.MaxValue;

            var conflictM = await _db.MaintenanceRecords.AnyAsync(m =>
                m.VehicleId == vehicleId &&
                (exceptId == null || m.MaintenanceRecordId != exceptId.Value) &&
                (m.EndAtUtc ?? DateTime.MaxValue) > s && e > m.StartAtUtc);

            var conflictRsv = await _db.Reservations.AnyAsync(r =>
                r.VehicleId == vehicleId &&
                r.Status != ReservationStatus.Cancelled &&
                r.ReservedToUtc > s && e > r.ReservedFromUtc);

            var conflictRent = await _db.Rentals.AnyAsync(r =>
                r.VehicleId == vehicleId &&
                (r.EndAtUtc ?? DateTime.MaxValue) > s &&
                r.StartAtUtc < e);

            return conflictM || conflictRsv || conflictRent;
        }

        private async Task SetVehicleStatusAsync(Guid vehicleId, VehicleStatus status)
        {
            var v = await _db.Vehicles.FirstAsync(x => x.VehicleId == vehicleId);
            if (v.Status != status)
            {
                v.Status = status;
                _db.Vehicles.Update(v);
                await _db.SaveChangesAsync();
            }
        }
    }
}
