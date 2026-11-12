using EDSOM.Models;
using EDSOM.Services;
using Microsoft.EntityFrameworkCore;

namespace EDSOM.Data
{
  
    // Se ejecuta al iniciar la aplicación.
    // se crean solos cuando se digita un funcionario con un departamento nuevo.)

    public static class DbSeeder
    {
        public static async Task SeedAsync(IServiceProvider services)
        {
            using var scope = services.CreateScope();
            var sp = scope.ServiceProvider;
            var context = sp.GetRequiredService<ApplicationDbContext>();
            var config = sp.GetRequiredService<IConfiguration>();
            var passwords = sp.GetRequiredService<PasswordTemporalService>();
            var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("DbSeeder");

            // Aplica las migraciones pendientes
            await context.Database.MigrateAsync();

            // Roles
            foreach (var nombre in RolesApp.Todos)
            {
                if (!await context.Roles.AnyAsync(r => r.Nombre == nombre))
                    context.Roles.Add(new Rol { Nombre = nombre });
            }
            await context.SaveChangesAsync();

            // 2) Administrador inicial (solo si todavía no existe ningún administrador)
            var rolAdmin = await context.Roles.FirstAsync(r => r.Nombre == RolesApp.Administrador);
            if (!await context.Usuarios.AnyAsync(u => u.RolId == rolAdmin.Id))
            {
                var nombreUsuario = config["AdminInicial:NombreUsuario"];
                if (string.IsNullOrWhiteSpace(nombreUsuario)) nombreUsuario = "admin";

                var password = config["AdminInicial:Password"];
                bool generada = string.IsNullOrWhiteSpace(password);
                if (generada) password = passwords.Generar(12);

                context.Usuarios.Add(new Usuario
                {
                    NombreUsuario = nombreUsuario,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                    CambiarPassword = true,
                    RolId = rolAdmin.Id
                });
                await context.SaveChangesAsync();

                if (generada)
                    logger.LogWarning("ADMIN INICIAL CREADO. Usuario: {Usuario} | Contraseña temporal: {Password} (se le pedirá cambiarla al ingresar)",
                        nombreUsuario, password);
                else
                    logger.LogWarning("ADMIN INICIAL CREADO con el usuario '{Usuario}' y la contraseña definida en AdminInicial:Password.", nombreUsuario);
            }
        }
    }
}
