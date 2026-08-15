namespace EXTRUDERNUCLEOS.Models
{
    public class DowntimeDetalle
    {
        public int Id { get; set; }              // Clave primaria
        public DateTime Fecha { get; set; }      // Día del registro
        public string CodigoImpresora { get; set; } // Código o nombre de la impresora
        public decimal Downtime { get; set; }   // ✅ decimal
    }

}