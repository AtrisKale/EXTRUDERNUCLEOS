using ClosedXML.Excel; // librería para Excel
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting.Internal;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics; // para Debug.WriteLine
using System.IO;       // para MemoryStream
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using EXTRUDERNUCLEOS.Models;

namespace EXTRUDERNUCLEOS.Controllers
{
    public class ImpresorasController : Controller
    {
        private readonly ApplicationDbContext _context;




        public ImpresorasController(ApplicationDbContext context)
        {
            _context = context;

        }



        // GET: Impresoras (solo los más recientes)
        public async Task<IActionResult> Index()
        {

            LimpiarComentariosAntiguos();

            var ahora = ObtenerHoraMatamoros();

            // 🔹 recalcular downtime acumulado desde BD
            //GuardarDowntimeAcumulado();

            // ========================================
            // CARGAR IMPRESORAS
            // ========================================
            var impresoras = await _context.Impresoras
                .Select(i => new Impresora
                {
                    Id = i.Id,
                    LocationExtru = i.LocationExtru ?? string.Empty,
                    Codigo = i.Codigo ?? string.Empty,
                    Tipo = i.Tipo ?? string.Empty,
                    Additive = i.Additive,
                    InkCoreRemainingHours = i.InkCoreRemainingHours,
                    Fecha = i.Fecha == default ? ahora : i.Fecha,
                    Hora = i.Hora == default ? TimeSpan.Zero : i.Hora,
                    Status = i.Status ?? string.Empty,
                    Downtime = i.Downtime > 1440 ? 0 : i.Downtime,
                    Comentario = i.LocationExtru != "TALLER DE IMPRESORAS" &&
                    i.Status == "PRODUCCION" &&
                    !string.IsNullOrWhiteSpace(i.Comentario) &&
                    i.Comentario.StartsWith("SE RETIRO DE")
                    ? "SIN COMENTARIOS"
                    : i.Comentario ?? string.Empty
                    })
                   .ToListAsync();

            // ========================================
            // ORDEN VISUAL POR UBICACIÓN
            // ========================================
            impresoras = impresoras
                .OrderBy(i =>
                    i.LocationExtru == "1" ? 1 :
                    i.LocationExtru == "2" ? 2 :
                    i.LocationExtru == "RETRABAJO" ? 3 :
                    i.LocationExtru == "3" ? 4 :
                    i.LocationExtru == "4" ? 5 :
                    i.LocationExtru == "5" ? 6 :
                    i.LocationExtru == "6" ? 7 :
                    i.LocationExtru == "TALLER DE IMPRESORAS" ? 8 :
                    99)
                .ThenBy(i => i.Id)
                .ToList();

            // ========================================
            // DOWNTIME DEL DÍA
            // ========================================
            var hoy = ObtenerFechaOperativa();

            foreach (var imp in impresoras)
            {
                imp.Downtime = _context.DowntimeDetalle
                    .Where(d =>
                        d.CodigoImpresora == imp.Codigo &&
                        d.Fecha.Date == hoy)
                    .Sum(d => (decimal?)d.Downtime) ?? 0;
            }

            // ========================================
            // VALIDAR SI NO HAY REGISTROS
            // ========================================
            if (impresoras == null || !impresoras.Any())
            {
                ViewBag.SinRegistros = true;

                // ⚡ Generar lista vacía con 11 impresoras inicializadas
                impresoras = Enumerable.Range(0, 11)
                    .Select(i => new Impresora
                    {
                        Codigo = string.Empty,
                        InkCoreRemainingHours = 0,
                        Downtime = 0,
                        Additive = false,
                        Fecha = ahora.Date,
                        Hora = TimeSpan.Zero,
                        LocationExtru = string.Empty,
                        Tipo = string.Empty,
                        Status = string.Empty,
                        Comentario = string.Empty
                    })
                    .ToList();
            }
            else
            {
                ViewBag.SinRegistros = false;
            }

            // ========================================
            // DATOS PARA GRÁFICA DE DOWNTIME
            // ========================================
            var inicioMes = new DateTime(
                ahora.Year,
                ahora.Month,
                1);

            var finMes = inicioMes
                .AddMonths(1)
                .AddDays(-1);

            var historial = _context.DowntimeHistorial
                .Where(d =>
                    d.Fecha >= inicioMes &&
                    d.Fecha <= finMes)
                .ToList();

            var datos = Enumerable
                .Range(0, (finMes - inicioMes).Days + 1)
                .Select(i =>
                {
                    var fecha = inicioMes.AddDays(i);

                    var registro = historial
                        .FirstOrDefault(x =>
                            x.Fecha.Date == fecha.Date);

                    var detalle = _context.DowntimeDetalle
                        .Where(det =>
                            det.Fecha.Date == fecha.Date)
                        .Select(det => new
                        {
                            CodigoImpresora = det.CodigoImpresora,
                            Downtime = det.Downtime
                        })
                        .ToList();

                    return new
                    {
                        Fecha = fecha.ToString("yyyy-MM-dd"),
                        Total = registro?.Valor ?? 0,
                        Detalle = detalle
                    };
                })
                .ToList();

            ViewBag.DowntimeData = datos;

            ViewBag.JsonDowntimeData =
                JsonConvert.SerializeObject(datos);

            // ========================================
            // ÚLTIMA EXPORTACIÓN
            // ========================================
            var ultimaExportacion =
                _context.Configuraciones
                    .FirstOrDefault(c =>
                        c.Clave == "UltimaExportacion");

            ViewBag.Mensaje =
                ultimaExportacion != null;

            if (ultimaExportacion != null)
            {
                ViewBag.UltimaExportacion =
                    ultimaExportacion.Valor;
            }

            // ========================================
            // RETURN
            // ========================================
            return View(impresoras);
        }



        private DateTime ObtenerFechaOperativa()
        {
           var ahora = ObtenerHoraMatamoros();

            // El día operativo comienza a las 7:00 AM
            if (ahora.TimeOfDay < TimeSpan.FromHours(8))
            {
                return ahora.Date.AddDays(-1);
            }

            return ahora.Date;
        }


















        private void RegistrarDowntimeHistorico(Impresora imp)
        {
            var hoy = ObtenerFechaOperativa();

            var historial = _context.DowntimeHistorial
                .FirstOrDefault(x => x.Fecha.Date == hoy);

            if (historial == null)
            {
                _context.DowntimeHistorial.Add(new DowntimeHistorial
                {
                    Fecha = hoy,
                    Valor = imp.Downtime
                });
            }
            else
            {
                historial.Valor += imp.Downtime;
            }

            var detalle = _context.DowntimeDetalle
                .FirstOrDefault(x =>
                    x.Fecha.Date == hoy &&
                    x.CodigoImpresora == imp.Codigo);

            if (detalle == null)
            {
                _context.DowntimeDetalle.Add(new DowntimeDetalle
                {
                    Fecha = hoy,
                    CodigoImpresora = imp.Codigo,
                    Downtime = imp.Downtime
                });
            }
            else
            {
                detalle.Downtime += imp.Downtime;
            }
        }

        /*
              public IActionResult GraficaDowntimeDesdeExcel()
                {
                    string rutaExcel = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "BASE_DE_DATOS3.xlsx");

                    if (!System.IO.File.Exists(rutaExcel))
                    {
                        ViewBag.Error = $"No se encontró el archivo en {rutaExcel}";
                        return View("Error");
                    }

                    var inicioMes = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                    var finMes = inicioMes.AddMonths(1).AddDays(-1);

                    using (var workbook = new XLWorkbook(rutaExcel))
                    {
                        var ws = workbook.Worksheet(1);

                        var datosExcel = ws.RangeUsed().RowsUsed()
                            .Select(r => new {
                                Fecha = DateTime.TryParse(r.Cell(6).GetValue<string>(), out var f) ? f.Date : DateTime.MinValue,
                                Downtime = double.TryParse(r.Cell(9).GetValue<string>(), out var dt) ? dt : 0,
                                Codigo = r.Cell(2).GetValue<string>()
                            })
                            .Where(x => x.Fecha != DateTime.MinValue && x.Downtime > 0)
                            .GroupBy(x => x.Fecha)
                            .Select(g => new {
                                Fecha = g.Key,
                                Total = Math.Min(1440, g.Sum(x => x.Downtime)),
                                Detalle = g.Select(x => new DowntimeDetalle
                                {
                                    Fecha = g.Key,
                                    CodigoImpresora = x.Codigo,
                                    Downtime = (int)x.Downtime
                                }).ToList()
                            })
                            .ToList();

                        var diasMes = Enumerable.Range(0, (finMes - inicioMes).Days + 1)
                            .Select(offset => inicioMes.AddDays(offset))
                            .ToList();

                        var datosCompletos = diasMes.Select(dia =>
                        {
                            var registro = datosExcel.FirstOrDefault(x => x.Fecha.Date == dia.Date);

                            return new
                            {
                                Fecha = dia.ToString("dd/MM/yyyy"),
                                Total = registro != null ? registro.Total : 0,
                                Detalle = registro != null ? registro.Detalle : new List<DowntimeDetalle>()
                            };
                        }).ToList();

                        // 🔧 Serializar aquí, no en la vista
                        var jsonOptions = new System.Text.Json.JsonSerializerOptions
                        {
                            ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles,
                            WriteIndented = false
                        };

                        ViewBag.JsonDowntimeData = System.Text.Json.JsonSerializer.Serialize(datosCompletos, jsonOptions);
                    }

                    return View();
                }

                */





        public IActionResult GraficaDowntime()
        {
            var ahora = ObtenerHoraMatamoros();
            var inicioMes = new DateTime(ahora.Year, ahora.Month, 1);
            var finMes = inicioMes.AddMonths(1).AddDays(-1);

            var datos = _context.DowntimeHistorial
    .Where(d => d.Fecha >= inicioMes && d.Fecha <= finMes)
    .OrderBy(d => d.Fecha)
    .Select(d => new {
        Fecha = d.Fecha,
        Total = d.Valor > 1440 ? 1440 : d.Valor,
        Detalle = _context.DowntimeDetalle
            .Where(det => det.Fecha == d.Fecha)
            .Select(det => new { det.CodigoImpresora, det.Downtime })
            .ToList()
    })
    .ToList();

            ViewBag.JsonDowntimeData = JsonConvert.SerializeObject(datos);


            ViewBag.DowntimeData = datos;
            return View();
        }




        public void GuardarDowntimeAcumulado()
        {
            var ahora = ObtenerHoraMatamoros();
            var inicioMes = new DateTime(ahora.Year, ahora.Month, 1);
            var finMes = inicioMes.AddMonths(1).AddDays(-1);



            var impresoras = _context.Impresoras
                .Where(i => i.Fecha >= inicioMes && i.Fecha <= finMes)
                .ToList();

            var acumulados = impresoras
                .GroupBy(i => i.Fecha.Date) // 🔧 agrupa por día exacto
                .Select(g => new
                {
                    Fecha = g.Key,
                    Total = Math.Min(1440, g.Sum(x => x.Downtime > 1440 ? 1440 : x.Downtime)),
                    Detalle = g.Where(x => x.Downtime > 0) // 🔧 solo impresoras con tiempo muerto
                               .Select(x => new { Codigo = x.Codigo, Downtime = x.Downtime })
                               .ToList()
                })
                .OrderBy(x => x.Fecha)
                .ToList();

            foreach (var d in acumulados)
            {
                var existente = _context.DowntimeHistorial.FirstOrDefault(x => x.Fecha == d.Fecha);
                if (existente != null)
                {
                    existente.Valor = d.Total;
                }
                else
                {
                    _context.DowntimeHistorial.Add(new DowntimeHistorial
                    {
                        Fecha = d.Fecha.Date,   // 🔧 fecha sin hora
                        Valor = d.Total
                    });
                }

                // 🔧 limpiar detalles previos de ese día para evitar duplicados
                var detallesPrevios = _context.DowntimeDetalle
                   .Where(x => x.Fecha.Date == d.Fecha.Date)
                   .ToList();

                if (detallesPrevios.Any())
                {
                    _context.DowntimeDetalle.RemoveRange(detallesPrevios);
                }

                foreach (var det in d.Detalle)
                {
                    _context.DowntimeDetalle.Add(new DowntimeDetalle
                    {
                        Fecha = d.Fecha.Date,   // 🔧 fecha sin hora
                        CodigoImpresora = det.Codigo,
                        Downtime = det.Downtime
                    });
                }


                // 🔧 depuración rápida
                Console.WriteLine($"Procesando día: {d.Fecha.ToShortDateString()} → Total {d.Total}");

            }

            _context.SaveChanges();
        }





        // GET: Historial (todos los registros)
        public async Task<IActionResult> Historial()
        {
            var ahora = ObtenerHoraMatamoros();
            var impresoras = await _context.Impresoras
                .OrderBy(i => i.Id)
                .ThenBy(i => i.Codigo ?? string.Empty)
                .Select(i => new Impresora
                {
                    Id = i.Id,
                    LocationExtru = i.LocationExtru ?? string.Empty,
                    Codigo = i.Codigo ?? string.Empty,
                    Tipo = i.Tipo ?? string.Empty,
                    Additive = i.Additive,
                    InkCoreRemainingHours = i.InkCoreRemainingHours,
                    Fecha = i.Fecha == default ? ahora : i.Fecha,
                    Hora = i.Hora == default ? TimeSpan.Zero : i.Hora,
                    Status = i.Status ?? string.Empty,

                    // Se carga primero en 0.
                    // Después se reemplaza con el acumulado de DowntimeDetalle.
                    Downtime = 0,

                    Comentario = i.Comentario ?? string.Empty
                })
                .ToListAsync();


            // ==========================================
            // CARGAR TIEMPO MUERTO HISTÓRICO
            // ==========================================
            foreach (var imp in impresoras)
            {
                imp.Downtime = _context.DowntimeDetalle
                    .Where(d =>
                        d.CodigoImpresora == imp.Codigo &&
                        d.Fecha.Date == imp.Fecha.Date)
                    .Sum(d => (decimal?)d.Downtime) ?? 0;
            }


            // ==========================================
            // ÚLTIMA EXPORTACIÓN
            // ==========================================
            var ultima = _context.Configuraciones
                .FirstOrDefault(c => c.Clave == "UltimaExportacion");

            ViewBag.UltimaExportacion = ultima?.Valor;

            return View(impresoras);
        }




        public IActionResult ExportarExcel()
        {
            var datos = _context.Impresoras.ToList();
            string rutaArchivo = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/videos/BASE_DE_DATOS3.xlsx");

            XLWorkbook workbook;
            IXLWorksheet hoja;

            if (System.IO.File.Exists(rutaArchivo))
            {
                // Abrir archivo existente
                workbook = new XLWorkbook(rutaArchivo);
                hoja = workbook.Worksheet("Registro de Impresoras");
            }
            else
            {
                // Crear nuevo archivo si no existe
                workbook = new XLWorkbook();
                hoja = workbook.Worksheets.Add("Registro de Impresoras");

                // Encabezados
                hoja.Cell(1, 1).Value = "Location Extruder";
                hoja.Cell(1, 2).Value = "Código";
                hoja.Cell(1, 3).Value = "Tipo";
                hoja.Cell(1, 4).Value = "Additive";
                hoja.Cell(1, 5).Value = "Horas núcleo restantes";
                hoja.Cell(1, 6).Value = "Fecha";
                hoja.Cell(1, 7).Value = "Hora";
                hoja.Cell(1, 8).Value = "Status";
                hoja.Cell(1, 9).Value = "Downtime";
                hoja.Cell(1, 10).Value = "Comentario";
            }

            // Buscar última fila usada
            int ultimaFila = hoja.LastRowUsed()?.RowNumber() ?? 1;

            foreach (var imp in datos)
            {
                ultimaFila++;
                hoja.Cell(ultimaFila, 1).Value = imp.LocationExtru;
                hoja.Cell(ultimaFila, 2).Value = imp.Codigo;
                hoja.Cell(ultimaFila, 3).Value = imp.Tipo;
                hoja.Cell(ultimaFila, 4).Value = imp.Additive;
                hoja.Cell(ultimaFila, 5).Value = imp.InkCoreRemainingHours;

                // ✅ Guardar Fecha en columna 6
                hoja.Cell(ultimaFila, 6).Value = imp.Fecha.ToString("dd/MM/yyyy");

                // ✅ Guardar Hora en columna 7 (solo una vez)
                hoja.Cell(ultimaFila, 7).Value = imp.Hora.ToString(@"hh\:mm\:ss");

                hoja.Cell(ultimaFila, 8).Value = imp.Status;
                hoja.Cell(ultimaFila, 9).Value = imp.Downtime;
                hoja.Cell(ultimaFila, 10).Value = imp.Comentario;
            }


            workbook.SaveAs(rutaArchivo);

            var content = System.IO.File.ReadAllBytes(rutaArchivo);
            return File(content,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "BASE_DE_DATOS3.xlsx");
        }






        // GET: Impresoras/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var impresora = await _context.Impresoras.FirstOrDefaultAsync(m => m.Id == id);
            if (impresora == null) return NotFound();

            return View(impresora);
        }

        // GET: Impresoras/Create
        public IActionResult Create()
        {
            return View();
        }


        public async Task<IActionResult> Status()
        {
            var ahora = ObtenerHoraMatamoros();
            var registros = await _context.Impresoras
                .Where(x => x.LocationExtru != "TALLER DE IMPRESORAS")
                .OrderBy(x => x.Id)
                .ToListAsync();

            var modelo = new List<Impresora>();

            // Función para crear un espacio visual vacío.
            // NO se guarda en la base de datos.
            Impresora Vacio(string ubicacion)
            {
                return new Impresora
                {
                    Id = 0,
                    LocationExtru = ubicacion,
                    Codigo = string.Empty,
                    Tipo = "VIDEOJET",
                    Additive = false,
                    InkCoreRemainingHours = 0,
                    Downtime = 0,
                    Fecha = ahora.Date,
                    Hora = TimeSpan.Zero,
                    Status = "PRODUCCION",
                    Comentario = "SIN COMENTARIOS"
                };
            }

            // =========================
            // EXTRUDER 1
            // =========================
            modelo.Add(
                registros.FirstOrDefault(x => x.LocationExtru == "1")
                ?? Vacio("1")
            );

            // =========================
            // EXTRUDER 2
            // =========================
            modelo.Add(
                registros.FirstOrDefault(x => x.LocationExtru == "2")
                ?? Vacio("2")
            );

            // =========================
            // RETRABAJO
            // =========================
            modelo.Add(
                registros.FirstOrDefault(x => x.LocationExtru == "RETRABAJO")
                ?? Vacio("RETRABAJO")
            );

            // =========================
            // EXTRUDER 3 - DOS ESPACIOS
            // =========================
            var ext3 = registros
           .Where(x => x.LocationExtru == "3")
           .OrderBy(x => EsTintaAmarilla(x) ? 1 : 0)
           .ThenBy(x => x.Id)
           .Take(2)
           .ToList();

            modelo.Add(ext3.ElementAtOrDefault(0) ?? Vacio("3"));
            modelo.Add(ext3.ElementAtOrDefault(1) ?? Vacio("3"));

            // =========================
            // EXTRUDER 4
            // =========================
            modelo.Add(
                registros.FirstOrDefault(x => x.LocationExtru == "4")
                ?? Vacio("4")
            );

            // =========================
            // EXTRUDER 5
            // =========================
            modelo.Add(
                registros.FirstOrDefault(x => x.LocationExtru == "5")
                ?? Vacio("5")
            );

            // =========================
            // EXTRUDER 6 - DOS ESPACIOS
            // =========================
            var ext6 = registros
            .Where(x => x.LocationExtru == "6")
            .OrderBy(x => EsTintaAmarilla(x) ? 1 : 0)
            .ThenBy(x => x.Id)
            .Take(2)
            .ToList();

            modelo.Add(ext6.ElementAtOrDefault(0) ?? Vacio("6"));
            modelo.Add(ext6.ElementAtOrDefault(1) ?? Vacio("6"));

            var ultima = _context.Configuraciones
                .FirstOrDefault(c => c.Clave == "UltimaExportacion");

            ViewBag.UltimaExportacion = ultima?.Valor;
            ViewBag.SinRegistros = false;



            // =====================================
            // IMPRESORAS DISPONIBLES EN TALLER
            // PARA EL MODAL DE MOVIMIENTO
            // =====================================
            var impresorasTaller = await _context.Impresoras
                .Where(x => x.LocationExtru == "TALLER DE IMPRESORAS")
                .OrderBy(x => x.Id)
                .ToListAsync();

            ViewBag.ImpresorasTaller = impresorasTaller;
            return View(modelo);
        }



        [HttpPost]
        public IActionResult ActualizarDesdeIndex(List<Impresora> impresoras)
        {
            foreach (var imp in impresoras)
            {
                var dbImp = _context.Impresoras.FirstOrDefault(x => x.Id == imp.Id);

                if (dbImp != null)
                {
                    dbImp.Comentario = imp.Comentario;
                    dbImp.Status = imp.Status;
                    dbImp.Codigo = imp.Codigo;
                    dbImp.InkCoreRemainingHours = imp.InkCoreRemainingHours;
                    dbImp.Additive = imp.Additive;

                    // NO registrar downtime aquí
                }
            }

            _context.SaveChanges();

            return RedirectToAction("Index");
        }




        //ACTUALIZAR DESDE STATUS
        [HttpPost]
        public IActionResult ActualizarTodo(List<Impresora> impresoras)
        {
            var ahora = ObtenerHoraMatamoros();


            // ============================================
            // GUARDAR LOS IDs QUE FUERON MOVIDOS
            // DESDE UN ESPACIO VISUAL VACÍO
            // ============================================
            var idsMovidos = new HashSet<int>();


            // ============================================
            // PRIMERA PASADA:
            // PROCESAR ESPACIOS VISUALES VACÍOS (Id = 0)
            // ============================================
            foreach (var imp in impresoras.Where(x => x.Id == 0))
            {
                // Si el espacio sigue vacío o tiene 0,
                // no hacemos nada.
                if (string.IsNullOrWhiteSpace(imp.Codigo) ||
                    imp.Codigo == "0")
                {
                    continue;
                }


                // ============================================
                // BUSCAR SI ESA IMPRESORA YA EXISTE
                // ============================================
                var impresoraExistente = _context.Impresoras
                    .FirstOrDefault(x => x.Codigo == imp.Codigo);


                if (impresoraExistente != null)
                {
                    // ============================================
                    // YA EXISTE:
                    // SOLO LA ESTAMOS MOVIENDO DE UBICACIÓN
                    // ============================================

                    Console.WriteLine(
                        $"MOVIENDO IMPRESORA {impresoraExistente.Codigo} " +
                        $"DE {impresoraExistente.LocationExtru} " +
                        $"A {imp.LocationExtru}");


                    // Guardamos su ID para impedir que
                    // su posición anterior la vuelva a sobrescribir.
                    idsMovidos.Add(impresoraExistente.Id);


                    impresoraExistente.LocationExtru =
                        imp.LocationExtru;

                    impresoraExistente.Tipo =
                        imp.Tipo;

                    impresoraExistente.Status =
                        string.IsNullOrWhiteSpace(imp.Status)
                        ? "PRODUCCION"
                        : imp.Status;

                    impresoraExistente.InkCoreRemainingHours =
                        imp.InkCoreRemainingHours;

                    impresoraExistente.Additive =
                        imp.Additive;

                    impresoraExistente.Downtime =
                        imp.Downtime;

                    impresoraExistente.Comentario =
                        string.IsNullOrWhiteSpace(imp.Comentario)
                        ? "SIN COMENTARIOS"
                        : imp.Comentario;

                    impresoraExistente.Fecha =
                        ahora.Date;

                    impresoraExistente.Hora =
                        ahora.TimeOfDay;


                    // ============================================
                    // REGISTRAR DOWNTIME SI EXISTE
                    // ============================================
                    if (imp.Downtime > 0)
                    {
                        RegistrarDowntimeHistorico(imp);

                        // Ya quedó en historial,
                        // así que reiniciamos el actual.
                        impresoraExistente.Downtime = 0;
                    }
                }
                else
                {
                    // ============================================
                    // NO EXISTE:
                    // ES CAPTURA INICIAL DE UNA BD VACÍA
                    // ============================================

                    var nuevaImpresora = new Impresora
                    {
                        LocationExtru =
                            imp.LocationExtru ?? string.Empty,

                        Codigo =
                            imp.Codigo ?? string.Empty,

                        Tipo =
                            imp.Tipo ?? string.Empty,

                        Additive =
                            imp.Additive,

                        InkCoreRemainingHours =
                            imp.InkCoreRemainingHours,

                        Fecha =
                            ahora.Date,

                        Hora =
                            ahora.TimeOfDay,

                        Status =
                            string.IsNullOrWhiteSpace(imp.Status)
                            ? "PRODUCCION"
                            : imp.Status,

                        // Si hay downtime lo registramos en historial,
                        // pero no lo dejamos acumulado en la fila.
                        Downtime = 0,

                        Comentario =
                            string.IsNullOrWhiteSpace(imp.Comentario)
                            ? "SIN COMENTARIOS"
                            : imp.Comentario
                    };


                    _context.Impresoras.Add(nuevaImpresora);


                    Console.WriteLine(
                        $"CREANDO REGISTRO INICIAL -> " +
                        $"CODIGO={imp.Codigo} " +
                        $"UBICACION={imp.LocationExtru}");


                    // ============================================
                    // DOWNTIME DURANTE CAPTURA INICIAL
                    // ============================================
                    if (imp.Downtime > 0)
                    {
                        RegistrarDowntimeHistorico(imp);
                    }
                }
            }



            // ============================================
            // SEGUNDA PASADA:
            // ACTUALIZAR REGISTROS QUE YA TIENEN ID
            // ============================================
            foreach (var imp in impresoras.Where(x => x.Id > 0))
            {
                var dbImp = _context.Impresoras
                    .FirstOrDefault(x => x.Id == imp.Id);


                if (dbImp == null)
                {
                    continue;
                }


                // ========================================
                // SI ESTA IMPRESORA FUE MOVIDA ARRIBA,
                // NO PERMITIR QUE SU POSICIÓN ANTERIOR
                // LA REGRESE
                // ========================================
                if (idsMovidos.Contains(dbImp.Id))
                {
                    Console.WriteLine(
                        $"ID {dbImp.Id} YA FUE MOVIDO. " +
                        $"SE IGNORA POSICIÓN ANTERIOR.");

                    continue;
                }


                // ========================================
                // PROTEGER IMPRESORAS DEL TALLER
                // ========================================
                if (dbImp.LocationExtru ==
                    "TALLER DE IMPRESORAS")
                {
                    continue;
                }


                Console.WriteLine(
                    $"ACTUALIZANDO ID={dbImp.Id} " +
                    $"CODIGO={imp.Codigo}");


                // ========================================
                // ACTUALIZACIÓN NORMAL
                // ========================================
                dbImp.LocationExtru =
                    imp.LocationExtru;

                dbImp.Tipo =
                    imp.Tipo;

                dbImp.Status =
                    imp.Status;

                dbImp.Comentario =
                    string.IsNullOrWhiteSpace(imp.Comentario)
                    ? "SIN COMENTARIOS"
                    : imp.Comentario;


                // Si ponen 0, considerarlo espacio vacío
                dbImp.Codigo =
                    imp.Codigo == "0"
                    ? string.Empty
                    : imp.Codigo;


                dbImp.InkCoreRemainingHours =
                    imp.InkCoreRemainingHours;

                dbImp.Downtime =
                    imp.Downtime;

                dbImp.Additive =
                    imp.Additive;


                // ========================================
                // REGISTRAR DOWNTIME
                // ========================================
                if (imp.Downtime > 0)
                {
                    RegistrarDowntimeHistorico(imp);

                    dbImp.Downtime = 0;
                }


                dbImp.Fecha =
                    ahora.Date;

                dbImp.Hora =
                   ahora.TimeOfDay;
            }



            // ============================================
            // GUARDAR TODO
            // ============================================
            _context.SaveChanges();


            // Mostrar modal después de guardar
            TempData["DatosActualizados"] = true;


            return RedirectToAction(nameof(Status));
        }


        private DateTime ObtenerHoraMatamoros()
        {
            string zona = OperatingSystem.IsWindows()
                ? "Central Standard Time"
                : "America/Matamoros";

            var zonaMatamoros = TimeZoneInfo.FindSystemTimeZoneById(zona);

            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zonaMatamoros);
        }



        private void LimpiarComentariosAntiguos()
        {
            var ahora = ObtenerHoraMatamoros();

            var impresoras = _context.Impresoras
                .Where(x =>
                    x.Comentario != null &&
                    x.Comentario != "" &&
                    x.Comentario != "SIN COMENTARIOS")
                .ToList();

            foreach (var imp in impresoras)
            {
                var comentario = imp.Comentario;

                if (comentario.StartsWith("[") &&
                    comentario.Length >= 12)
                {
                    var textoFecha = comentario.Substring(1, 10);

                    if (DateTime.TryParseExact(
                        textoFecha,
                        "dd/MM/yyyy",
                        null,
                        System.Globalization.DateTimeStyles.None,
                        out DateTime fechaComentario))
                    {
                        if (fechaComentario.Date <= ahora.Date.AddDays(-7))
                        {
                            imp.Comentario = "SIN COMENTARIOS";
                        }
                    }
                }
            }

            _context.SaveChanges();
        }

        private bool EsTintaAmarilla(Impresora imp)
        {
            return new[] { "13", "16" }.Contains(imp.Codigo);
        }


        [HttpPost]
        public IActionResult MoverImpresora(
       int idDanada,
       int idReemplazo,
       decimal downtime,
       string motivoCambio)
        {
            var ahora = ObtenerHoraMatamoros();

            var origen = _context.Impresoras
                .FirstOrDefault(x => x.Id == idDanada);

            var reemplazo = _context.Impresoras
                .FirstOrDefault(x => x.Id == idReemplazo);

            if (origen == null || reemplazo == null)
            {
                TempData["ErrorMovimiento"] =
                    "No se pudo encontrar una de las impresoras.";

                return RedirectToAction(nameof(Status));
            }

            if (origen.Id == reemplazo.Id)
            {
                TempData["ErrorMovimiento"] =
                    "Debes seleccionar una impresora diferente.";

                return RedirectToAction(nameof(Status));
            }


            // Guardar las dos ubicaciones ANTES de mover nada
            var ubicacionOrigen = origen.LocationExtru;
            var ubicacionReemplazo = reemplazo.LocationExtru;


            // =====================================================
            // CASO 1
            // LAS DOS ESTÁN EN PRODUCCIÓN
            //
            // Solo intercambiar posiciones.
            // NINGUNA se manda al Taller.
            // =====================================================

            if (ubicacionOrigen != "TALLER DE IMPRESORAS" &&
                ubicacionReemplazo != "TALLER DE IMPRESORAS")
            {
                origen.LocationExtru = ubicacionReemplazo;
                reemplazo.LocationExtru = ubicacionOrigen;

                origen.Status = "PRODUCCION";
                reemplazo.Status = "PRODUCCION";

                origen.Fecha = ahora.Date;
                origen.Hora = ahora.TimeOfDay;

                reemplazo.Fecha = ahora.Date;
                reemplazo.Hora = ahora.TimeOfDay;


                // No estamos reportando una falla.
                // Por lo tanto NO se manda ninguna al taller.
                // Tampoco generamos comentario de retiro.

                _context.SaveChanges();

                TempData["MovimientoRealizado"] = true;
                TempData["TipoMovimiento"] = "NORMAL";

                return RedirectToAction(nameof(Status));
            }



            // =====================================================
            // CASO 2
            // REEMPLAZO DESDE EL TALLER
            //
            // Aquí sí conservamos la lógica de falla/reemplazo.
            // =====================================================

            if (ubicacionReemplazo == "TALLER DE IMPRESORAS")
            {
                var ubicacionDestino = ubicacionOrigen;


                // -----------------------------------------
                // REGISTRAR DOWNTIME
                // -----------------------------------------

                if (downtime > 0)
                {
                    origen.Downtime = downtime;

                    RegistrarDowntimeHistorico(origen);

                    origen.Downtime = 0;
                }


                // -----------------------------------------
                // COMENTARIO DE FALLA
                // -----------------------------------------

                if (!string.IsNullOrWhiteSpace(motivoCambio))
                {
                    origen.Comentario =
                        $"SE RETIRO DE {ubicacionDestino}: " +
                        $"{motivoCambio.Trim()} - " +
                        $"[{ahora:dd/MM/yyyy}]";
                }
                else
                {
                    origen.Comentario = "SIN COMENTARIOS";
                }


                // -----------------------------------------
                // MANDAR LA DAÑADA AL TALLER
                // -----------------------------------------

                origen.LocationExtru =
                    "TALLER DE IMPRESORAS";

                origen.Status =
                    "MAINTENANCE";

                origen.Fecha =
                    ahora.Date;

                origen.Hora =
                    ahora.TimeOfDay;


                // -----------------------------------------
                // SACAR REEMPLAZO DEL TALLER
                // Y MANDARLO A PRODUCCIÓN
                // -----------------------------------------

                reemplazo.LocationExtru =
                    ubicacionDestino;

                reemplazo.Status =
                    "PRODUCCION";

                reemplazo.Fecha =
                    ahora.Date;

                reemplazo.Hora =
                    ahora.TimeOfDay;

                // Al regresar del taller ya no debe
                // conservar el comentario de falla anterior.
                reemplazo.Comentario =
                    "SIN COMENTARIOS";


                _context.SaveChanges();

                _context.SaveChanges();

                TempData["MovimientoRealizado"] = true;
                TempData["TipoMovimiento"] = "FALLA";

               

                return RedirectToAction(nameof(Status));
            }


            // Si llegó alguna combinación no contemplada
            TempData["ErrorMovimiento"] =
                "No se pudo realizar el movimiento.";

            return RedirectToAction(nameof(Status));
        }






        [HttpPost]
        public IActionResult AgregarAlTaller(int id)
        {
            var ahora = ObtenerHoraMatamoros();

            var impresora = _context.Impresoras
                .FirstOrDefault(x => x.Id == id);

            if (impresora == null)
            {
                return RedirectToAction("Taller", "Mtto");
            }

            // Solo actualizamos el registro existente
            impresora.LocationExtru = "TALLER DE IMPRESORAS";
            impresora.Status = "MAINTENANCE";
            impresora.Fecha = ahora.Date;
            impresora.Hora = ahora.TimeOfDay;

            _context.SaveChanges();

            return RedirectToAction("Taller", "Mtto");
        }




        [HttpPost]
        public IActionResult RetirarDelTaller(
            int id,
            string nuevaUbicacion,
            int? idDesplazada,
            string? ubicacionDesplazada)
        {
            var ahora = ObtenerHoraMatamoros();

            var impresora = _context.Impresoras
                .FirstOrDefault(x => x.Id == id);

            if (impresora == null)
                return RedirectToAction("Taller", "Mtto");

            if (impresora.LocationExtru != "TALLER DE IMPRESORAS")
                return RedirectToAction("Taller", "Mtto");


            // ==========================================
            // BUSCAR IMPRESORAS QUE YA ESTÁN EN DESTINO
            // ==========================================
            var ocupantes = _context.Impresoras
                .Where(x =>
                    x.LocationExtru == nuevaUbicacion &&
                    x.Id != impresora.Id)
                .OrderBy(x => x.Id)
                .ToList();


            // Ext #3 y Ext #6 tienen capacidad para 2
            int capacidad =
                nuevaUbicacion == "3" ||
                nuevaUbicacion == "6"
                    ? 2
                    : 1;


            // ==========================================
            // SI EL DESTINO ESTÁ LLENO
            // ==========================================
            if (ocupantes.Count >= capacidad)
            {
                // Todavía no sabemos qué hacer con
                // la impresora que está ocupando el lugar.
                if (!idDesplazada.HasValue ||
                    string.IsNullOrWhiteSpace(ubicacionDesplazada))
                {
                    TempData["RetiroPendienteId"] = id;
                    TempData["RetiroDestino"] = nuevaUbicacion;

                    return RedirectToAction("Taller", "Mtto");
                }


                // Buscar exactamente la impresora
                // que el usuario decidió mover.
                var desplazada = _context.Impresoras
                    .FirstOrDefault(x => x.Id == idDesplazada.Value);

                if (desplazada == null)
                    return RedirectToAction("Taller", "Mtto");


                // Mover la que actualmente ocupa el lugar
                desplazada.LocationExtru = ubicacionDesplazada;
                desplazada.Status = "PRODUCCION";
                desplazada.Fecha = ahora.Date;
                desplazada.Hora = ahora.TimeOfDay;
            }


            // ==========================================
            // REGRESAR LA REPARADA A PRODUCCIÓN
            // ==========================================
            impresora.LocationExtru = nuevaUbicacion;
            impresora.Status = "PRODUCCION";
            impresora.Fecha = ahora.Date;
            impresora.Hora = ahora.TimeOfDay;

            // Limpiar comentario de la falla anterior
            impresora.Comentario = "SIN COMENTARIOS";

            _context.SaveChanges();

            TempData["RetiroRealizado"] = true;

            return RedirectToAction("Taller", "Mtto");
        }




        [HttpPost]
        public IActionResult GuardarTaller(
        [Bind(Prefix = "impresoras")] List<Impresora> impresoras)
        {
            Console.WriteLine("===== ENTRO A GUARDAR TALLER =====");

            if (!ModelState.IsValid)
            {
                Console.WriteLine("MODELSTATE INVALIDO");
            }
            
            var ahora = ObtenerHoraMatamoros();
            
            foreach (var imp in impresoras)
            {
                Console.WriteLine(
                    $"ID={imp.Id} CODIGO={imp.Codigo} DOWNTIME={imp.Downtime}");

                var dbImp = _context.Impresoras
                    .FirstOrDefault(x => x.Id == imp.Id);

                if (dbImp != null)
                {
                    // Si ponen 0, considerarlo como espacio sin impresora
                    dbImp.Codigo = imp.Codigo == "0"
                        ? string.Empty
                        : imp.Codigo;

                    dbImp.InkCoreRemainingHours = imp.InkCoreRemainingHours;

                    // Guardar checkbox Aditivo
                    dbImp.Additive = imp.Additive;

                    dbImp.Downtime = imp.Downtime;
                    dbImp.Comentario = imp.Comentario;
                    dbImp.Fecha = ahora.Date;
                    dbImp.Hora = ahora.TimeOfDay;

                    if (imp.Downtime > 0)
                    {
                        Console.WriteLine(
                            $"REGISTRANDO HISTORIAL -> {imp.Codigo} = {imp.Downtime}");

                        RegistrarDowntimeHistorico(imp);

                        dbImp.Downtime = 0;
                    }
                }
            }

            _context.SaveChanges();

            ViewBag.Mensaje = true;

            var modelo = _context.Impresoras
                .OrderBy(m => m.Id)
                .ToList();

            return View("~/Views/Mtto/Taller.cshtml", modelo);
        }





        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,LocationExtru,Codigo,Tipo,Additive,InkCoreRemainingHours,Fecha,Hora,Status,Downtime,Comentario")] Impresora impresora)
        {
            if (id != impresora.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(impresora);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ImpresoraExists(impresora.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(impresora);
        }

        private bool ImpresoraExists(int id)
        {
            return _context.Impresoras.Any(e => e.Id == id);
        }
    }
}