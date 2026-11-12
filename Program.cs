using System.Security.Claims;
using EDSOM.Data;
using EDSOM.Models;
using EDSOM.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

//  Base de datos 
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("ConexionSQL")));

//  Cifrado reversible de las contraseñas temporales
builder.Services.AddDataProtection();
builder.Services.AddSingleton<PasswordTemporalService>();

//  Autenticación por cookie
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Seguridad/Login";
        options.AccessDeniedPath = "/Seguridad/AccesoDenegado";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.Name = "EDSOM.Auth";

        // En cada pantalla se confirma que el usuario siga existiendo, no esté bloqueado y
        // conserve el mismo rol. Así si el admin cambia un rol o bloquea a alguien
        // efecto de inmediato (la persona debe volver a iniciar sesión).
        options.Events.OnValidatePrincipal = async context =>
        {
            if (Path.HasExtension(context.Request.Path.Value)) return; // 

            var rolEnSesion = context.Principal?.FindFirstValue(ClaimTypes.Role);
            bool valido = false;

            if (int.TryParse(context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var usuarioId))
            {
                var db = context.HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
                var estado = await db.Usuarios.AsNoTracking()
                    .Where(u => u.Id == usuarioId)
                    .Select(u => new { u.Bloqueado, Rol = u.Rol!.Nombre })
                    .FirstOrDefaultAsync();

                valido = estado != null && !estado.Bloqueado && estado.Rol == rolEnSesion;
            }

            if (!valido)
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
        };
    });

//  MVC: todo el sistema exige sesión
//      fuerza el cambio de contraseña temporal antes de usar cualquier pantalla.
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AuthorizeFilter());
    options.Filters.Add<CambioPasswordObligatorioFilter>();
});

var app = builder.Build();

//  Aplica migraciones y crea roles, departamentos de ejemplo y el admin inicial
await DbSeeder.SeedAsync(app.Services);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
