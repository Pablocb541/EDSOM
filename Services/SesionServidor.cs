namespace EDSOM.Services
{
    /// <summary>
    /// Identificador único que se genera cada vez que la aplicación arranca.
    /// Se guarda en la sesión (cookie) al iniciar sesión; si el servidor se reinicia, el
    /// valor cambia y las sesiones viejas dejan de ser válidas (se vuelve al login).
    /// </summary>
    public static class SesionServidor
    {
        public static readonly string ArranqueId = Guid.NewGuid().ToString("N");
    }
}
