using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace EDSOM.Services
{
    /// <summary>
    /// Genera contraseñas temporales aleatorias y las cifra/descifra con ASP.NET Core Data Protection.
    /// (BCrypt es de un solo sentido, por eso la temporal se guarda además cifrada: así el
    /// administrador puede consultarla hasta que el funcionario la cambie.)
    /// </summary>
    public class PasswordTemporalService
    {
        private const string Mayusculas = "ABCDEFGHJKLMNPQRSTUVWXYZ";   // sin I, O
        private const string Minusculas = "abcdefghijkmnopqrstuvwxyz";  // sin l
        private const string Digitos = "23456789";                      // sin 0, 1
        private const string Todos = Mayusculas + Minusculas + Digitos;

        private readonly IDataProtector _protector;

        public PasswordTemporalService(IDataProtectionProvider provider)
        {
            _protector = provider.CreateProtector("EDSOM.PasswordTemporal.v1");
        }

        /// <summary>Contraseña aleatoria criptográficamente segura, con mayúscula, minúscula y número.</summary>
        public string Generar(int longitud = 10)
        {
            var chars = new List<char>
            {
                Mayusculas[RandomNumberGenerator.GetInt32(Mayusculas.Length)],
                Minusculas[RandomNumberGenerator.GetInt32(Minusculas.Length)],
                Digitos[RandomNumberGenerator.GetInt32(Digitos.Length)]
            };

            while (chars.Count < longitud)
                chars.Add(Todos[RandomNumberGenerator.GetInt32(Todos.Length)]);

            // Mezclar (Fisher-Yates) para que los 3 primeros no estén siempre en la misma posición
            for (int i = chars.Count - 1; i > 0; i--)
            {
                int j = RandomNumberGenerator.GetInt32(i + 1);
                (chars[i], chars[j]) = (chars[j], chars[i]);
            }

            return new string(chars.ToArray());
        }

        public string Cifrar(string textoPlano) => _protector.Protect(textoPlano);

        /// <summary>Devuelve null si no se puede descifrar (por ejemplo, si se perdieron las llaves).</summary>
        public string? Descifrar(string textoCifrado)
        {
            try { return _protector.Unprotect(textoCifrado); }
            catch (CryptographicException) { return null; }
        }
    }
}
