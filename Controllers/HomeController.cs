using EXTRUDERNUCLEOS.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using EXTRUDERNUCLEOS.Models;

namespace EXTRUDERNUCLEOS.Controllers
{
    public class HomeController : Controller
    {


        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View(); // Renderiza Views/Account/Login.cshtml
        }

        [HttpPost]
        public IActionResult Login(Contraseña model)
        {
            if (ModelState.IsValid)
            {
                // Validación simple de ejemplo
                if (model.Username == "admin" && model.Password == "tgna1400")
                {
                    return RedirectToAction("Index", "Home");
                }

                ViewBag.ErrorMessage = "Contraseña equivocada. Intenta de nuevo.";

            }

            return View(model);
        }




        [HttpPost]
        public IActionResult ActualizarInformacion(IFormCollection form)
        {
            // 🔹 Aquí recibes todos los valores de la tabla
            // Ejemplo: form["Celda_0_0"], form["Celda_3_5"], etc.

            // Puedes recorrerlos y guardarlos en base de datos o procesarlos
            foreach (var key in form.Keys)
            {
                var valor = form[key];
                // Guardar o procesar valor
            }

            ViewBag.Mensaje = "Información actualizada correctamente.";
            return View("Index");
        }


        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}