namespace EXTRUDERNUCLEOS.Models
{
    public class DowntimeData
    {
        public DateTime Fecha { get; set; }
        public decimal Valor { get; set; }   // acumulado por día
    }
}
