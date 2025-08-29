using Microsoft.AspNetCore.Identity;
using VRSproject.Models;

namespace VRSproject.Data;

public class SeedRolesAndAdmin
{
    private readonly RoleManager<IdentityRole> _roleMgr;
    private readonly UserManager<ApplicationUser> _userMgr;
    public SeedRolesAndAdmin(RoleManager<IdentityRole> roleMgr, UserManager<ApplicationUser> userMgr)
    { _roleMgr = roleMgr; _userMgr = userMgr; }

    public async Task RunAsync(IConfiguration cfg)
    {
        foreach (var r in new[] { "Admin", "Customer" })
            if (!await _roleMgr.RoleExistsAsync(r))
                await _roleMgr.CreateAsync(new IdentityRole(r));

        var email = cfg["Admin:Email"] ?? "admin@vrs.local";
        var pwd = cfg["Admin:Password"] ?? "Admin#12345";

        var admin = await _userMgr.FindByEmailAsync(email);
        if (admin == null)
        {
            admin = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
            await _userMgr.CreateAsync(admin, pwd);
            await _userMgr.AddToRoleAsync(admin, "Admin");
        }
    }
}
