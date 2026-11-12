namespace EDSOM.Models
{
    // Datos que necesita la pantalla de inicio 
    public class InicioViewModel
    {
        public string NombreCompleto { get; set; } = "";

        // Funcionario asociado al usuario que inició sesión
        public Funcionario? Funcionario { get; set; }
    }
}
