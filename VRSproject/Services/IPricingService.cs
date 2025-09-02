using VRSproject.Models;

namespace VRSproject.Services
{
    public class IPricingService
    {
        Task<decimal> QuoteAsync(Guid vehicleId, DateTime fromUtc, DateTime toUtc, PricingType pricingType);
    }
}
