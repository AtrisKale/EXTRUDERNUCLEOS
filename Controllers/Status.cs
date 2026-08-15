using Microsoft.AspNetCore.Mvc;

namespace EXTRUDERNUCLEOS.Controllers
{
    public class Status : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}