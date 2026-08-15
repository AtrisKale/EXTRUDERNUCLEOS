using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EXTRUDERNUCLEOS.Models
{
    public class Impresora
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        // Campos de texto: nunca nulos, default = ""
        public string LocationExtru { get; set; } = string.Empty;
        public string Codigo { get; set; } = string.Empty;
        public string Tipo { get; set; } = string.Empty;
        public bool Additive { get; set; }

        public string Status { get; set; } = string.Empty;
        public string Comentario { get; set; } = string.Empty;

        // Campos numéricos: nunca nulos, default = 0
        public int InkCoreRemainingHours { get; set; } = 0;
        public decimal Downtime { get; set; }   // ✅ decimal


        // Campos de fecha/hora: nunca nulos, default = valores seguros
        public DateTime Fecha { get; set; } = DateTime.Now;
        public TimeSpan Hora { get; set; } = TimeSpan.Zero;
    }
}