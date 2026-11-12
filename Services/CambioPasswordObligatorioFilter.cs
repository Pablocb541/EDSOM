using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace EDSOM.Services
{
    /// <summary>
    /// Si el usuario inició sesión con una contraseña temporal, no puede usar ninguna otra
    /// pantalla del sistema hasta que la cambie (solo CambiarPassword y Logout).
    /// </summary>
    public class CambioPasswordObligatorioFilter : IActionFilter
    {
        public void OnActionExecuting(ActionExecutingContext context)
        {
            var user = context.HttpContext.User;
            if (user.Identity?.IsAuthenticated != true || !user.HasClaim("CambiarPassword", "true"))
                return;

            var controlador = context.RouteData.Values["controller"]?.ToString();
            var accion = context.RouteData.Values["action"]?.ToString();

            bool permitido = string.Equals(controlador, "Seguridad", StringComparison.OrdinalIgnoreCase)
                && (string.Equals(accion, "CambiarPassword", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(accion, "Logout", StringComparison.OrdinalIgnoreCase));

            if (!permitido)
                context.Result = new RedirectToActionResult("CambiarPassword", "Seguridad", null);
        }

        public void OnActionExecuted(ActionExecutedContext context) { }
    }
}
