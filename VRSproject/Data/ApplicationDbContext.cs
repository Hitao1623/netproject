using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using VRSproject.Models;

namespace VRSproject.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<Car> Cars => Set<Car>();
    public DbSet<Bike> Bikes => Set<Bike>();
    public DbSet<Truck> Trucks => Set<Truck>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<Rental> Rentals => Set<Rental>();
    public DbSet<MaintenanceRecord> MaintenanceRecords => Set<MaintenanceRecord>();
    public DbSet<Feedback> Feedbacks => Set<Feedback>();
    public DbSet<PricingPlan> PricingPlans => Set<PricingPlan>();
    public DbSet<TypeBaseRate> TypeBaseRates => Set<TypeBaseRate>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<Vehicle>().HasDiscriminator<string>("Discriminator")
            .HasValue<Car>("Car").HasValue<Bike>("Bike").HasValue<Truck>("Truck");

        b.Entity<Truck>().Property(x => x.CargoCapacity).HasPrecision(10, 2);
        b.Entity<Reservation>().Property(x => x.QuoteAmount).HasPrecision(10, 2);
        b.Entity<Rental>().Property(x => x.TotalCost).HasPrecision(10, 2);
        b.Entity<PricingPlan>().Property(x => x.RateMultiplier).HasPrecision(10, 2);
        b.Entity<TypeBaseRate>().Property(x => x.BaseRate).HasPrecision(10, 2);

        b.Entity<Reservation>()
            .HasOne(x => x.Vehicle).WithMany(x => x.Reservations).HasForeignKey(x => x.VehicleId);
        b.Entity<Reservation>()
            .HasOne<ApplicationUser>(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId);

        b.Entity<Rental>()
            .HasOne(x => x.Vehicle).WithMany(x => x.Rentals).HasForeignKey(x => x.VehicleId);
        b.Entity<Rental>()
            .HasOne<ApplicationUser>(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId);
        b.Entity<Rental>()
            .HasOne(x => x.Reservation).WithMany().HasForeignKey(x => x.ReservationId).IsRequired(false);

        b.Entity<Feedback>()
            .HasOne(x => x.Vehicle).WithMany(x => x.Feedbacks).HasForeignKey(x => x.VehicleId);
        b.Entity<Feedback>()
            .HasOne<ApplicationUser>(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId);
        b.Entity<Feedback>()
            .HasOne(x => x.Rental).WithMany(x => x.Feedbacks).HasForeignKey(x => x.RentalId).IsRequired();

        b.Entity<MaintenanceRecord>()
            .HasOne(x => x.Vehicle).WithMany(x => x.MaintenanceRecords).HasForeignKey(x => x.VehicleId);

        b.Entity<PricingPlan>()
            .HasOne(x => x.Vehicle).WithMany().HasForeignKey(x => x.VehicleId).IsRequired(false);
   
        b.Entity<Feedback>()
            .HasIndex(x => new { x.RentalId }).IsUnique(); 

        b.Entity<PricingPlan>()
            .HasIndex(x => new { x.VehicleId, x.PricingType, x.IsActive, x.EffectiveFromUtc, x.EffectiveToUtc });

        b.Entity<PricingPlan>()
            .HasIndex(x => new { x.VehicleType, x.PricingType, x.IsActive, x.EffectiveFromUtc, x.EffectiveToUtc });

        b.Entity<TypeBaseRate>()
            .HasIndex(x => new { x.VehicleType, x.PricingType, x.IsActive, x.EffectiveFromUtc, x.EffectiveToUtc });

        b.Entity<Feedback>().ToTable(t => t.HasCheckConstraint("CK_Feedback_Rating", "Rating BETWEEN 1 AND 5"));
    }
}
