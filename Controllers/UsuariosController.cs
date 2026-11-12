using EDSOM.Models;
using EDSOM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EDSOM.Controllers
{
    // Administración de usuarios.
    //  - Administrador:      todo (cambiar roles, ver/regenerar contraseñas, bloquear, ver accesos).
    //  - Salud Ocupacional:  solo consulta: ver la lista de usuarios (usuario, estado), ver contraseñas
    //                        temporales (excepto las de administradores) y ver accesos. NO puede cambiar
    //                        roles, regenerar contraseñas ni bloquear.

    [Authorize(Roles = RolesApp.Gestion)]
    public class UsuariosController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly PasswordTemporalService _passwords;

        public UsuariosController(ApplicationDbContext context, PasswordTemporalService passwords)
        {
            _context = context;
            _passwords = passwords;
        }

        public async Task<IActionResult> Index(string? buscar)
        {
            var query = _context.Usuarios
                .Include(u => u.Rol)
                .Include(u => u.Funcionario)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(buscar))
            {
                buscar = buscar.Trim();
                query = query.Where(u => u.NombreUsuario.Contains(buscar)
                                      || (u.Funcionario != null && u.Funcionario.NombreCompleto.Contains(buscar)));
            }

            ViewBag.Buscar = buscar;
            ViewBag.Roles = await _context.Roles.OrderBy(r => r.Nombre).ToListAsync();

            return View(await query.OrderBy(u => u.NombreUsuario).ToListAsync());
        }

        // registro accesos     
        public async Task<IActionResult> Accesos(string? buscar, DateTime? desde, DateTime? hasta)
        {
            var query = _context.AccesosLogs
                .Include(a => a.Usuario).ThenInclude(u => u!.Funcionario)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(buscar))
            {
                buscar = buscar.Trim();
                query = query.Where(a => a.Usuario!.NombreUsuario.Contains(buscar)
                    || (a.Usuario!.Funcionario != null && a.Usuario.Funcionario.NombreCompleto.Contains(buscar)));
            }
            if (desde.HasValue)
                query = query.Where(a => a.FechaHora >= desde.Value.Date);
            if (hasta.HasValue)
                query = query.Where(a => a.FechaHora < hasta.Value.Date.AddDays(1));

            ViewBag.Buscar = buscar;
            ViewBag.Desde = desde?.ToString("yyyy-MM-dd");
            ViewBag.Hasta = hasta?.ToString("yyyy-MM-dd");

            // Se muestran los accesos más recientes que cumplan el filtro
            var lista = await query.OrderByDescending(a => a.FechaHora).Take(200).ToListAsync();
            return View(lista);
        }

        // cambiar  rol
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = RolesApp.Administrador)]
        public async Task<IActionResult> CambiarRol(int id, int rolId)
        {
            var usuario = await _context.Usuarios.Include(u => u.Rol).FirstOrDefaultAsync(u => u.Id == id);
            var rol = await _context.Roles.FindAsync(rolId);
            if (usuario == null || rol == null) return NotFound();

            if (usuario.NombreUsuario == User.Identity?.Name)
            {
                TempData["Error"] = "No puede cambiar su propio rol.";
                return RedirectToAction(nameof(Index));
            }

            usuario.RolId = rol.Id;
            await _context.SaveChangesAsync();

            // El usuario afectado deberá iniciar sesión de nuevo (se valida en cada solicitud).
            TempData["Exito"] = $"Rol de {usuario.NombreUsuario} cambiado a «{rol.Nombre}».";
            return RedirectToAction(nameof(Index));
        }

        // ver contraseña temporalt
        // POST (no GET) para que la contraseña no quede en historial caché.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> VerPasswordTemporal(int id)
        {
            var usuario = await _context.Usuarios
                .Include(u => u.Funcionario)
                .Include(u => u.Rol)
                .FirstOrDefaultAsync(u => u.Id == id);
            if (usuario == null) return NotFound();

            if (!User.IsInRole(RolesApp.Administrador) && usuario.Rol?.Nombre == RolesApp.Administrador)
            {
                TempData["Error"] = "Solo un administrador puede ver la contraseña de otro administrador.";
                return RedirectToAction(nameof(Index));
            }

            if (!usuario.CambiarPassword || string.IsNullOrEmpty(usuario.PasswordTemporalCifrada))
            {
                TempData["Error"] = "Este usuario ya cambió su contraseña (o no tiene una temporal guardada). Si la olvidó, use «Regenerar».";
                return RedirectToAction(nameof(Index));
            }

            var password = _passwords.Descifrar(usuario.PasswordTemporalCifrada);
            if (password == null)
            {
                TempData["Error"] = "No se pudo descifrar la contraseña temporal. Use «Regenerar» para crear una nueva.";
                return RedirectToAction(nameof(Index));
            }

            return View("PasswordTemporal", new PasswordTemporalViewModel
            {
                UsuarioId = usuario.Id,
                NombreUsuario = usuario.NombreUsuario,
                NombreCompleto = usuario.Funcionario?.NombreCompleto,
                Password = password
            });
        }

        // regenerar contraseñaa
        [HttpPost]
        [ValidateAntiForgeryToken]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        [Authorize(Roles = RolesApp.Administrador)]
        public async Task<IActionResult> RegenerarPassword(int id)
        {
            var usuario = await _context.Usuarios
                .Include(u => u.Funcionario)
                .FirstOrDefaultAsync(u => u.Id == id);
            if (usuario == null) return NotFound();

            var nueva = _passwords.Generar();
            usuario.PasswordHash = BCrypt.Net.BCrypt.HashPassword(nueva);
            usuario.PasswordTemporalCifrada = _passwords.Cifrar(nueva);
            usuario.CambiarPassword = true;
            await _context.SaveChangesAsync();

            return View("PasswordTemporal", new PasswordTemporalViewModel
            {
                UsuarioId = usuario.Id,
                NombreUsuario = usuario.NombreUsuario,
                NombreCompleto = usuario.Funcionario?.NombreCompleto,
                Password = nueva,
                EsNueva = true
            });
        }

        // bloquear usuario oa 
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = RolesApp.Administrador)]
        public async Task<IActionResult> CambiarBloqueo(int id)
        {
            var usuario = await _context.Usuarios.FindAsync(id);
            if (usuario == null) return NotFound();

            if (usuario.NombreUsuario == User.Identity?.Name)
            {
                TempData["Error"] = "No puede bloquear su propia cuenta.";
                return RedirectToAction(nameof(Index));
            }

            usuario.Bloqueado = !usuario.Bloqueado;
            await _context.SaveChangesAsync();

            TempData["Exito"] = usuario.Bloqueado
                ? $"Usuario {usuario.NombreUsuario} bloqueado."
                : $"Usuario {usuario.NombreUsuario} desbloqueado.";
            return RedirectToAction(nameof(Index));
        }
    }
}