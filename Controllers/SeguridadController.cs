using System.Security.Claims;
using EDSOM.Models;
using EDSOM.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EDSOM.Controllers
{
    public class SeguridadController : Controller
    {
        private readonly ApplicationDbContext _context;

        public SeguridadController(ApplicationDbContext context)
        {
            _context = context;
        }

        // login

        [AllowAnonymous]
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction("Index", "Home");

            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string nombreUsuario, string password, string? returnUrl = null)
        {
            nombreUsuario = (nombreUsuario ?? "").Trim();

            var usuario = await _context.Usuarios
                .Include(u => u.Rol)
                .Include(u => u.Funcionario)
                .FirstOrDefaultAsync(u => u.NombreUsuario == nombreUsuario);

            bool credencialesValidas = usuario != null
                && !usuario.Bloqueado
                && BCrypt.Net.BCrypt.Verify(password ?? "", usuario.PasswordHash);

            if (!credencialesValidas)
            {
                ViewBag.Error = "Usuario o contraseña incorrectos.";
                ViewBag.ReturnUrl = returnUrl;
                return View();
            }

            _context.AccesosLogs.Add(new AccesoLog { UsuarioId = usuario!.Id, FechaHora = DateTime.Now });
            await _context.SaveChangesAsync();

            await IniciarSesionAsync(usuario);

            // Primer ingreso o contraseña regenerada obligar a cambiarla.
            if (usuario.CambiarPassword)
                return RedirectToAction(nameof(CambiarPassword));

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return LocalRedirect(returnUrl);

            return RedirectToAction("Index", "Home");
        }

        // cambiar constraseña

        // el usuario se toma de la sesión,
        // nadie puede cambiar la contraseña de otra persona.
        [Authorize]
        [HttpGet]
        public IActionResult CambiarPassword()
        {
            ViewBag.Forzado = User.HasClaim("CambiarPassword", "true");
            return View(new CambiarPasswordViewModel());
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CambiarPassword(CambiarPasswordViewModel modelo)
        {
            var usuario = await ObtenerUsuarioActualAsync();
            if (usuario == null)
            {
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return RedirectToAction(nameof(Login));
            }

            ViewBag.Forzado = usuario.CambiarPassword;

            if (!ModelState.IsValid)
                return View(modelo);

            if (!BCrypt.Net.BCrypt.Verify(modelo.PasswordActual, usuario.PasswordHash))
            {
                ModelState.AddModelError(nameof(modelo.PasswordActual), "La contraseña actual es incorrecta.");
                return View(modelo);
            }

            if (BCrypt.Net.BCrypt.Verify(modelo.NuevaPassword, usuario.PasswordHash))
            {
                ModelState.AddModelError(nameof(modelo.NuevaPassword), "La nueva contraseña debe ser diferente a la actual.");
                return View(modelo);
            }

            if (modelo.NuevaPassword.Contains(usuario.NombreUsuario, StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(nameof(modelo.NuevaPassword), "La contraseña no puede contener su nombre de usuario (cédula).");
                return View(modelo);
            }

            usuario.PasswordHash = BCrypt.Net.BCrypt.HashPassword(modelo.NuevaPassword);
            usuario.CambiarPassword = false;
            usuario.PasswordTemporalCifrada = null;   // la temporal ya no es válida ni consultable
            await _context.SaveChangesAsync();

            // Renovar la sesión sin la marca de "cambio obligatorio"
            await IniciarSesionAsync(usuario);

            TempData["Exito"] = "Contraseña actualizada correctamente.";
            return RedirectToAction("Index", "Home");
        }

        // 

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(Login));
        }

        [AllowAnonymous]
        public IActionResult AccesoDenegado()
        {
            return View();
        }

        // 

        private async Task IniciarSesionAsync(Usuario usuario)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
                new Claim(ClaimTypes.Name, usuario.NombreUsuario),
                new Claim(ClaimTypes.Role, usuario.Rol!.Nombre),
                new Claim("FuncionarioId", usuario.FuncionarioId ?? ""),
                new Claim("NombreCompleto", usuario.Funcionario?.NombreCompleto ?? usuario.NombreUsuario),
                new Claim("CambiarPassword", usuario.CambiarPassword ? "true" : "false"),
                new Claim("ArranqueId", SesionServidor.ArranqueId)
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
        }

        private async Task<Usuario?> ObtenerUsuarioActualAsync()
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id))
                return null;

            return await _context.Usuarios
                .Include(u => u.Rol)
                .Include(u => u.Funcionario)
                .FirstOrDefaultAsync(u => u.Id == id && !u.Bloqueado);
        }
    }
}