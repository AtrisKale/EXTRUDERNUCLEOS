using Microsoft.EntityFrameworkCore;
using System.Reflection.Emit;

namespace EXTRUDERNUCLEOS.Models
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // Aquí defines tus tablas como DbSet
        public DbSet<Impresora> Impresoras { get; set; }
        public DbSet<DowntimeDetalle> DowntimeDetalle { get; set; }

        public DbSet<DowntimeHistorial> DowntimeHistorial { get; set; }
        public DbSet<Bitacora> Bitacoras { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Configuración de precisión para evitar truncamientos
            modelBuilder.Entity<DowntimeHistorial>()
                .Property(d => d.Valor)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Impresora>()
                .Property(i => i.Downtime)
                .HasPrecision(18, 2);

            base.OnModelCreating(modelBuilder);
        }

        public DbSet<Configuracion> Configuraciones { get; set; }

    }
}



