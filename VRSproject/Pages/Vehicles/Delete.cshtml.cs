using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using VRSproject.Models;
using VRSproject.Services;

namespace VRSproject.Pages.Vehicles
{
    [Authorize(Roles = "Admin")]
    public class DeleteModel : PageModel
    {
        private readonly VehicleService _svc;
        public DeleteModel(VehicleService svc) => _svc = svc;

        [BindProperty(SupportsGet = true)]
        public Guid Id { get; set; }

        public Vehicle? Vehicle { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            Vehicle = await _svc.GetAsync(Id);
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            try
            {
                await _svc.DeleteAsync(Id);
                return RedirectToPage("Index");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                Vehicle = await _svc.GetAsync(Id);
                return Page();
            }
        }
    }
}
