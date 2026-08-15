using System;

namespace EXTRUDERNUCLEOS.Models
{
    public class DowntimeHistorial
    {
        public int Id { get; set; }          // clave primaria
        public DateTime Fecha { get; set; }  // día
        public decimal Valor { get; set; }   // ✅ decimal para homogeneidad
    }
}