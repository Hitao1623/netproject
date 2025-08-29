namespace VRSproject.Models;

public abstract class Vehicle
{
    public Guid VehicleId { get; set; }
    public VehicleType VehicleType { get; set; }
    public string Make { get; set; } = "";
    public string Model { get; set; } = "";
    public int Year { get; set; }
    public VehicleStatus Status { get; set; } = VehicleStatus.Available;

    
    public ICollection<Rental> Rentals { get; set; } = new List<Rental>();
    public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
    public ICollection<Feedback> Feedbacks { get; set; } = new List<Feedback>();
    public ICollection<MaintenanceRecord> MaintenanceRecords { get; set; } = new List<MaintenanceRecord>();
}

public class Car : Vehicle { public int NumberOfSeats { get; set; } }
public class Bike : Vehicle { public string BikeType { get; set; } = ""; }
public class Truck : Vehicle { public decimal CargoCapacity { get; set; } }  // precision in OnModelCreating

public class Reservation
{
    public Guid ReservationId { get; set; }
    public Guid VehicleId { get; set; }
    public string CustomerId { get; set; } = ""; // FK -> AspNetUsers.Id (string)
    public DateTime ReservedFromUtc { get; set; }
    public DateTime ReservedToUtc { get; set; }
    public ReservationStatus Status { get; set; } = ReservationStatus.Pending;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public decimal QuoteAmount { get; set; }

    public Vehicle? Vehicle { get; set; }
    public ApplicationUser? Customer { get; set; }
}

public class Rental
{
    public Guid RentalId { get; set; }
    public Guid VehicleId { get; set; }
    public string CustomerId { get; set; } = "";
    public Guid? ReservationId { get; set; }  // walk-in: null
    public DateTime StartAtUtc { get; set; }
    public DateTime? EndAtUtc { get; set; }
    public PricingType PricingType { get; set; }
    public decimal TotalCost { get; set; }
    public RentalStatus Status { get; set; } = RentalStatus.Active;

    public Vehicle? Vehicle { get; set; }
    public ApplicationUser? Customer { get; set; }
    public Reservation? Reservation { get; set; }
    public ICollection<Feedback> Feedbacks { get; set; } = new List<Feedback>();
}

public class MaintenanceRecord
{
    public Guid MaintenanceRecordId { get; set; }
    public Guid VehicleId { get; set; }
    public DateTime StartAtUtc { get; set; }
    public DateTime? EndAtUtc { get; set; }
    public string Description { get; set; } = "";

    public Vehicle? Vehicle { get; set; }
}

public class Feedback
{
    public Guid FeedbackId { get; set; }
    public Guid VehicleId { get; set; }
    public string CustomerId { get; set; } = "";
    public Guid RentalId { get; set; }            
    public int Rating { get; set; }               // 1 to 5
    public string Comment { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }

    public Vehicle? Vehicle { get; set; }
    public ApplicationUser? Customer { get; set; }
    public Rental? Rental { get; set; }
}

public class PricingPlan
{
    public Guid PricingPlanId { get; set; }
    public VehicleType VehicleType { get; set; }  
    public Guid? VehicleId { get; set; }          
    public PricingType PricingType { get; set; }   // Hourly/Daily/Weekly
    public decimal RateMultiplier { get; set; }    // >0
    public DateTime? EffectiveFromUtc { get; set; }
    public DateTime? EffectiveToUtc { get; set; }
    public bool IsActive { get; set; } = true;

    public Vehicle? Vehicle { get; set; }
}

public class TypeBaseRate
{
    public int Id { get; set; }
    public VehicleType VehicleType { get; set; }
    public PricingType PricingType { get; set; }
    public decimal BaseRate { get; set; }
    public DateTime? EffectiveFromUtc { get; set; }
    public DateTime? EffectiveToUtc { get; set; }
    public bool IsActive { get; set; } = true;
}
