using Microsoft.EntityFrameworkCore;

namespace EDSOM.Models
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // Módulo de Seguridad
        public DbSet<Rol> Roles { get; set; }
        public DbSet<Usuario> Usuarios { get; set; }
        public DbSet<AccesoLog> AccesosLogs { get; set; }

        // Módulo de Expediente
        public DbSet<Funcionario> Funcionarios { get; set; }
        public DbSet<Departamento> Departamentos { get; set; }

        // Req-003: Signos vitales y consulta general
        public DbSet<ConsultaSignosVitales> ConsultasSignosVitales { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Rol>()
                .HasIndex(r => r.Nombre)
                .IsUnique();

            // No puede haber dos departamentos con el mismo nombre
            modelBuilder.Entity<Departamento>()
                .HasIndex(d => d.Nombre)
                .IsUnique();

            modelBuilder.Entity<ConsultaSignosVitales>(e =>
            {
                // Un funcionario con historial de consultas NO se puede borrar por accidente.
                e.HasOne(c => c.Funcionario)
                    .WithMany()
                    .HasForeignKey(c => c.FuncionarioId)
                    .OnDelete(DeleteBehavior.Restrict);

                // Búsquedas frecuentes: historial por funcionario y filtros por fecha
                e.HasIndex(c => new { c.FuncionarioId, c.Fecha });
                e.HasIndex(c => c.Fecha);
            });

            modelBuilder.Entity<Usuario>(e =>
            {
                // La cédula es el nombre de usuario: debe ser única.
                e.HasIndex(u => u.NombreUsuario).IsUnique();

                // Un funcionario solo puede tener un usuario.
                e.HasIndex(u => u.FuncionarioId).IsUnique();

                // Borrar un rol NO debe borrar a todos los usuarios que lo tienen.
                e.HasOne(u => u.Rol)
                    .WithMany(r => r.Usuarios)
                    .HasForeignKey(u => u.RolId)
                    .OnDelete(DeleteBehavior.Restrict);

                // Un funcionario con usuario no se puede borrar "por accidente":
                // el controlador elimina primero el usuario de forma explícita.
                e.HasOne(u => u.Funcionario)
                    .WithMany(f => f.Usuarios)
                    .HasForeignKey(u => u.FuncionarioId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}
