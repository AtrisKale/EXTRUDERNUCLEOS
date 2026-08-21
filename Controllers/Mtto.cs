using Microsoft.AspNetCore.Mvc;
using EXTRUDERNUCLEOS.Models;
using ClosedXML.Excel;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace EXTRUDERNUCLEOS.Controllers
{
    public class MttoController : Controller
    {
        private readonly ApplicationDbContext _context;
        private static string _filebrowserToken = string.Empty;

        public MttoController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            return View();
        }




        private void CargarImpresorasBitacora()
        {
            ViewBag.ImpresorasBitacora = _context.Impresoras
                .Where(x =>
                    !string.IsNullOrWhiteSpace(x.Codigo) &&
                    (x.Tipo == "VIDEOJET" || x.Tipo == "LINX"))
                .ToList();
        }





        //Bitacora de mantenimiento preventivo y correctivo
        public IActionResult Bitacora(bool editar = false, int? filtroId = null)
        {
            IQueryable<Bitacora> registros = _context.Bitacoras;

            if (filtroId.HasValue && filtroId.Value > 0)
            {
                registros = registros
                    .Where(r => r.IdVideojet == filtroId.Value);
            }

            var lista = registros
                .OrderByDescending(r => r.Id)
                .Take(8)
                .ToList();

            ViewBag.ModoEdicion = editar;
            ViewBag.FiltroId = filtroId;
            ViewBag.SinRegistros = !lista.Any();


            // =========================================
            // LLENAR SELECT VIDEOJET + LINX
            // =========================================

            CargarImpresorasBitacora();


            return View(lista);
        }




        [HttpPost]
        public IActionResult GuardarBitacora(Bitacora nuevo, List<Bitacora> bitacora)


        {
            var ahora = ObtenerHoraMatamoros();

            // ==========================
            // NUEVO REGISTRO
            // ==========================
            if (nuevo != null &&
               (
                   nuevo.IdVideojet > 0 ||
                   !string.IsNullOrWhiteSpace(nuevo.MotivoMtto) ||
                   !string.IsNullOrWhiteSpace(nuevo.Procedimiento) ||
                   !string.IsNullOrWhiteSpace(nuevo.Turno) ||
                   !string.IsNullOrWhiteSpace(nuevo.Pendientes)
               ))
            {
                nuevo.Fecha = nuevo.Fecha == DateTime.MinValue
                    ? ahora
                    : nuevo.Fecha;

                _context.Bitacoras.Add(nuevo);
            }

            // ==========================
            // EDICIÓN DE HISTORIAL
            // ==========================
            if (bitacora != null)
            {
                foreach (var item in bitacora)
                {
                    if (item.Id <= 0)
                        continue;

                    var registro = _context.Bitacoras
                        .FirstOrDefault(x => x.Id == item.Id);

                    if (registro == null)
                        continue;

                    bool modificado = false;

                    if (registro.IdVideojet != item.IdVideojet)
                    {
                        registro.IdVideojet = item.IdVideojet;
                        modificado = true;
                    }

                    if (registro.MotivoMtto != item.MotivoMtto)
                    {
                        registro.MotivoMtto = item.MotivoMtto;
                        modificado = true;
                    }

                    if (registro.Procedimiento != item.Procedimiento)
                    {
                        registro.Procedimiento = item.Procedimiento;
                        modificado = true;
                    }

                    if (registro.Turno != item.Turno)
                    {
                        registro.Turno = item.Turno;
                        modificado = true;
                    }

                    if (registro.Pendientes != item.Pendientes)
                    {
                        registro.Pendientes = item.Pendientes;
                        modificado = true;
                    }

                    if (modificado)
                    {
                        _context.Bitacoras.Update(registro);
                    }
                }
            }

            _context.SaveChanges();



            ViewBag.Mensaje = true;

            var lista = _context.Bitacoras
            .OrderByDescending(x => x.Id)
            .Take(8)
            .ToList();

            ViewBag.ModoEdicion = false;
            ViewBag.SinRegistros = !lista.Any();

            return RedirectToAction(nameof(Bitacora));
        }



        public IActionResult HistorialBitacora(int? filtroId = null)
        {

        
            var hoy =ObtenerHoraMatamoros();

            var registros = _context.Bitacoras
                .Where(x => x.Fecha.Month == hoy.Month &&
                            x.Fecha.Year == hoy.Year);

            if (filtroId.HasValue && filtroId.Value > 0)
            {
                registros = registros.Where(x => x.IdVideojet == filtroId.Value);
            }

            var lista = registros
                .OrderByDescending(x => x.Id)
                .ToList();

            ViewBag.FiltroId = filtroId;

            if (!lista.Any())
            {
                ViewBag.SinRegistros = true;
            }

            return View(lista);
        }



        public IActionResult HistorialMes()
        {
            var hoy = ObtenerHoraMatamoros();

            var registros = _context.Impresoras
                .Where(x => x.Fecha.Month == hoy.Month
                         && x.Fecha.Year == hoy.Year)
                .OrderByDescending(x => x.Fecha)
                .ThenByDescending(x => x.Hora)
                .ToList();

            return View(registros);
        }





        [HttpPost]
        public IActionResult DescargarExcelFiltrado(
    bool IdVideojet,
    bool MotivoMtto,
    bool Procedimiento,
    bool Fecha,
    bool Turno,
    bool Pendientes,
    bool Todo,
    string TipoExportacion,
    bool FiltrarVideojet,
    int? FiltroVideojet,
    bool FiltrarFechas,
    DateTime? FechaInicio,
    DateTime? FechaFin)
        {
            var consulta = _context.Bitacoras.AsQueryable();

            // ==========================
            // MES ACTUAL O HISTORIAL
            // ==========================

            // Solo aplicar "Mes actual" cuando NO se esté
            // utilizando un filtro personalizado.
            if (TipoExportacion == "Mes" &&
                !FiltrarVideojet &&
                !FiltrarFechas)
            {
                var hoy = ObtenerHoraMatamoros();

                consulta = consulta.Where(x =>
                    x.Fecha.Month == hoy.Month &&
                    x.Fecha.Year == hoy.Year);
            }

            // ==========================
            // FILTRO POR VIDEOJET
            // ==========================
            if (FiltrarVideojet &&
                FiltroVideojet.HasValue)
            {
                consulta = consulta.Where(x =>
                    x.IdVideojet.HasValue &&
                    x.IdVideojet.Value == FiltroVideojet.Value);
            }

            // ==========================
            // FILTRO POR FECHAS
            // ==========================
            if (FiltrarFechas)
            {
                if (FechaInicio.HasValue)
                {
                    consulta = consulta.Where(x =>
                        x.Fecha >= FechaInicio.Value.Date);
                }

                if (FechaFin.HasValue)
                {
                    consulta = consulta.Where(x =>
                        x.Fecha < FechaFin.Value.Date.AddDays(1));
                }
            }

            // ==========================
            // OBTENER RESULTADOS
            // ==========================
            var registros = consulta
                .OrderByDescending(x => x.Fecha)
                .ToList();


            // ==========================
            // VALIDAR FILTRO VIDEOJET
            // ==========================
            if (FiltrarVideojet &&
                FiltroVideojet.HasValue)
            {
                bool existeVideojet = _context.Bitacoras.Any(x =>
                    x.IdVideojet.HasValue &&
                    x.IdVideojet.Value == FiltroVideojet.Value);

                if (!existeVideojet)
                {
                    TempData["ErrorExportacion"] =
                        $"No se encontraron registros para el ID Videojet {FiltroVideojet.Value}.";

                    return RedirectToAction("HistorialBitacora");
                }
            }


            // ==========================
            // VALIDAR VIDEOJET + FECHA
            // ==========================
            if (FiltrarVideojet &&
                FiltroVideojet.HasValue &&
                FiltrarFechas &&
                registros.Count == 0)
            {
                string inicio = FechaInicio.HasValue
                    ? FechaInicio.Value.ToString("dd/MM/yyyy")
                    : "inicio";

                string fin = FechaFin.HasValue
                    ? FechaFin.Value.ToString("dd/MM/yyyy")
                    : "fin";

                TempData["ErrorExportacion"] =
                    $"El ID Videojet {FiltroVideojet.Value} existe, pero no tiene registros entre {inicio} y {fin}.";

                return RedirectToAction("HistorialBitacora");
            }


            // ==========================
            // VALIDAR SOLO FECHAS
            // ==========================
            if (!FiltrarVideojet &&
                FiltrarFechas &&
                registros.Count == 0)
            {
                string inicio = FechaInicio.HasValue
                    ? FechaInicio.Value.ToString("dd/MM/yyyy")
                    : "inicio";

                string fin = FechaFin.HasValue
                    ? FechaFin.Value.ToString("dd/MM/yyyy")
                    : "fin";

                TempData["ErrorExportacion"] =
                    $"No se encontraron registros en el rango de fechas {inicio} a {fin}.";

                return RedirectToAction("HistorialBitacora");
            }


            // ==========================
            // VALIDACIÓN GENERAL
            // ==========================
            if (registros.Count == 0)
            {
                TempData["ErrorExportacion"] =
                    "No se encontraron registros con los filtros seleccionados.";

                return RedirectToAction("HistorialBitacora");
            }


            // ==========================
            // VALIDAR QUE EXISTA
            // AL MENOS UNA COLUMNA
            // ==========================
            if (!Todo &&
                !IdVideojet &&
                !MotivoMtto &&
                !Procedimiento &&
                !Fecha &&
                !Turno &&
                !Pendientes)
            {
                TempData["ErrorExportacion"] =
                    "Selecciona al menos una columna para exportar.";

                return RedirectToAction("HistorialBitacora");
            }


            // ==========================
            // CREAR EXCEL
            // ==========================
            using (var workbook = new XLWorkbook())
            {
                var ws = workbook.Worksheets.Add("Bitacora");

                int columna = 1;

                // ==========================
                // ENCABEZADOS
                // ==========================
                if (Todo || IdVideojet)
                    ws.Cell(1, columna++).Value = "ID Videojet";

                if (Todo || MotivoMtto)
                    ws.Cell(1, columna++).Value = "Motivo de mtto";

                if (Todo || Procedimiento)
                    ws.Cell(1, columna++).Value = "Procedimiento";

                if (Todo || Fecha)
                    ws.Cell(1, columna++).Value = "Fecha";

                if (Todo || Turno)
                    ws.Cell(1, columna++).Value = "Turno";

                if (Todo || Pendientes)
                    ws.Cell(1, columna++).Value = "Pendientes";


                // ==========================
                // DATOS
                // ==========================
                int fila = 2;

                foreach (var item in registros)
                {
                    int col = 1;

                    if (Todo || IdVideojet)
                        ws.Cell(fila, col++).Value =
                            item.IdVideojet?.ToString() ?? "";

                    if (Todo || MotivoMtto)
                        ws.Cell(fila, col++).Value =
                            item.MotivoMtto ?? "";

                    if (Todo || Procedimiento)
                        ws.Cell(fila, col++).Value =
                            item.Procedimiento ?? "";

                    if (Todo || Fecha)
                        ws.Cell(fila, col++).Value =
                            item.Fecha.ToString("dd/MM/yyyy");

                    if (Todo || Turno)
                        ws.Cell(fila, col++).Value =
                            item.Turno ?? "";

                    if (Todo || Pendientes)
                        ws.Cell(fila, col++).Value =
                            item.Pendientes ?? "";

                    fila++;
                }

                ws.Columns().AdjustToContents();

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);

                    return File(
                        stream.ToArray(),
                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        $"Bitacora_{ObtenerHoraMatamoros:yyyyMMdd_HHmmss}.xlsx");
                }
            }
        }



        public IActionResult Taller()
        {
            var ahora = ObtenerHoraMatamoros();
            
            var impresoras = _context.Impresoras.ToList();

            // Si no existe Videojet en Taller, crear uno
            if (!impresoras.Any(x => x.LocationExtru == "TALLER DE IMPRESORAS" && x.Tipo == "VIDEOJET"))
            {
                _context.Impresoras.Add(new Impresora
                {
                    LocationExtru = "TALLER DE IMPRESORAS",
                    Tipo = "VIDEOJET",
                    Status = "MAINTENANCE",
                    Codigo = string.Empty,
                    InkCoreRemainingHours = 0,
                    Downtime = 0,
                    Comentario = "SIN COMENTARIOS",
                    Fecha = ahora.Date,
                    Hora = ahora.TimeOfDay
                });
                _context.SaveChanges();
            }

            // Si no existe Linx en Taller, crear uno
            if (!impresoras.Any(x => x.LocationExtru == "TALLER DE IMPRESORAS" && x.Tipo == "LINX"))
            {
                _context.Impresoras.Add(new Impresora
                {
                    LocationExtru = "TALLER DE IMPRESORAS",
                    Tipo = "LINX",
                    Status = "MAINTENANCE",
                    Codigo = string.Empty,
                    InkCoreRemainingHours = 0,
                    Downtime = 0,
                    Comentario = "SIN COMENTARIOS",
                    Fecha = ahora.Date,
                    Hora = ahora.TimeOfDay
                });
                _context.SaveChanges();
            }

            var modelo = _context.Impresoras.ToList();

            // 👇 Mostrar siempre 0 en los inputs
            foreach (var imp in modelo)
            {
                imp.Downtime = 0;
            }

            return View(modelo);
        }

        private DateTime ObtenerHoraMatamoros()
        {
            string zona = OperatingSystem.IsWindows()
                ? "Central Standard Time"
                : "America/Matamoros";

            var zonaMatamoros = TimeZoneInfo.FindSystemTimeZoneById(zona);

            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zonaMatamoros);
        }

        public IActionResult Videos()
        {

            return View();
        }

        // GET: /Mtto/ObtenerVideoMantenimiento
        [HttpGet("Mtto/ObtenerVideoMantenimiento")]
        public async Task<IActionResult> ObtenerVideoMantenimiento()
        {
            string urlBase = "http://10.195.250.100:7000";

            try
            {
                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(30);

                    // 🔐 PASO 1: Login automático de administración de Filebrowser
                    if (string.IsNullOrEmpty(_filebrowserToken))
                    {
                        var loginData = new { username = "admin", password = "admin" };
                        var jsonPayload = new StringContent(System.Text.Json.JsonSerializer.Serialize(loginData), System.Text.Encoding.UTF8, "application/json");

                        var loginResponse = await client.PostAsync($"{urlBase}/api/login", jsonPayload);
                        if (loginResponse.IsSuccessStatusCode)
                        {
                            _filebrowserToken = await loginResponse.Content.ReadAsStringAsync();
                            _filebrowserToken = _filebrowserToken.Trim('"');
                        }
                    }

                    // 📹 PASO 2: Intento de descarga desde Filebrowser
                    if (!string.IsNullOrEmpty(_filebrowserToken))
                    {
                        client.DefaultRequestHeaders.Add("X-Auth", _filebrowserToken);

                        string urlArchivo = $"{urlBase}/api/raw/RecursosMtto/v88_final.mp4";

                        var response = await client.GetAsync(urlArchivo, HttpCompletionOption.ResponseHeadersRead);

                        if (response.IsSuccessStatusCode)
                        {
                            var stream = await response.Content.ReadAsStreamAsync();
                            return File(stream, "video/mp4", enableRangeProcessing: true);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Loguear error si es necesario
            }

            return NotFound("El video v88_final.mp4 no se encuentra disponible en Filebrowser.");
        }

    }
}