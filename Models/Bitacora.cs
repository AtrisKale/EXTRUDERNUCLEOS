using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EXTRUDERNUCLEOS.Models
{
    public class Bitacora
    {
        [Key]
        public int BitacoraId { get; set; }
        public int? IdVideojet { get; set; }   // antes era int
        public string? MotivoMtto { get; set; }
        public string? Procedimiento { get; set; }

        public string? Turno { get; set; }
        public string? Pendientes { get; set; }


        // ✅ Ahora es obligatorio
        public DateTime Fecha { get; set; }




        // 🔧 Campos adicionales para sincronizar con Impresoras
        [NotMapped]
        public string
            Codigo
        { get; set; }

        [NotMapped]
        public int InkCoreRemainingHours { get; set; }

        [NotMapped]
        public decimal Downtime { get; set; }

        [NotMapped]
        public string Comentario { get; set; }

        [NotMapped]
        public string LocationExtru { get; set; } = string.Empty;





    }

}