
using System.ComponentModel.DataAnnotations;

namespace EXTRUDERNUCLEOS.Models
{
    public class Contraseña
    {
        [Required(ErrorMessage = "El usuario es obligatorio")]
        public string Username { get; set; }

        [Required(ErrorMessage = "La contraseña es obligatoria")]
        [DataType(DataType.Password)]
        public string Password { get; set; }
    }
}
