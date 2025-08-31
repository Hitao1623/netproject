using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using VRSproject.Data;
using VRSproject.Models;
using VRSproject.Services;

var builder = WebApplication.CreateBuilder(args);

// MySQL + EF Core (Pomelo)
var conn = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(opt =>
    opt.UseMySql(conn, ServerVersion.AutoDetect(conn)));

// Identity + Roles
builder.Services.AddDefaultIdentity<ApplicationUser>(o =>
{
    o.SignIn.RequireConfirmedAccount = false;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddRazorPages();

builder.Services.AddTransient<SeedRolesAndAdmin>();
builder.Services.AddScoped<VehicleService>();
builder.Services.AddScoped<MaintenanceService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

// run seed
using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<SeedRolesAndAdmin>();
    await seeder.RunAsync(builder.Configuration);
}

app.Run();
