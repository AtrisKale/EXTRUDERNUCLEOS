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
            // 🔹 recalcular downtime acumulado desde BD
            //GuardarDowntimeAcumulado();

            var impresoras = await _context.Impresoras
                .Select(i => new Impresora
                {
                    Id = i.Id,
                    LocationExtru = i.LocationExtru ?? string.Empty,
                    Codigo = i.Codigo ?? string.Empty,
                    Tipo = i.Tipo ?? string.Empty,
                    Additive = i.Additive,
                    InkCoreRemainingHours = i.InkCoreRemainingHours,
                    Fecha = i.Fecha == default ? DateTime.Now : i.Fecha,
                    Hora = i.Hora == default ? TimeSpan.Zero : i.Hora,
                    Status = i.Status ?? string.Empty,
                    Downtime = i.Downtime > 1440 ? 0 : i.Downtime,
                    Comentario = i.Comentario ?? string.Empty
                })
                .OrderBy(i => i.Id)   // ✅ solo por Id
                .ToListAsync();

            var hoy = DateTime.Today;

            foreach (var imp in impresoras)
            {
                imp.Downtime = _context.DowntimeDetalle
                    .Where(d =>
                        d.CodigoImpresora == imp.Codigo &&
                        d.Fecha.Date == hoy)
                    .Sum(d => (decimal?)d.Downtime) ?? 0;
            }


            // ✅ Avisar si no hay registros
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
                        Fecha = DateTime.Now.Date,
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

            // 🔧 traer historial con Fecha como DateTime
            // ========================================
            // DATOS PARA GRÁFICA DE DOWNTIME
            // ========================================

            var inicioMes = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            var finMes = inicioMes.AddMonths(1).AddDays(-1);



            var historial = _context.DowntimeHistorial
                .Where(d => d.Fecha >= inicioMes && d.Fecha <= finMes)
                .ToList();

            var datos = Enumerable.Range(0, (finMes - inicioMes).Days + 1)
                .Select(i =>
                {
                    var fecha = inicioMes.AddDays(i);

                    var registro = historial
                        .FirstOrDefault(x => x.Fecha.Date == fecha.Date);

                    var detalle = _context.DowntimeDetalle
                        .Where(det => det.Fecha.Date == fecha.Date)
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
            ViewBag.JsonDowntimeData = JsonConvert.SerializeObject(datos);

            var ultimaExportacion = _context.Configuraciones
                .FirstOrDefault(c => c.Clave == "UltimaExportacion");

            ViewBag.Mensaje = ultimaExportacion != null;
            if (ultimaExportacion != null)
                ViewBag.UltimaExportacion = ultimaExportacion.Valor;

            // ✅ único return al final
            return View(impresoras);
        }





        private void RegistrarDowntimeHistorico(Impresora imp)
        {
            var hoy = DateTime.Now.Date;

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
            var inicioMes = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
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
            var inicioMes = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
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
                    Fecha = i.Fecha == default ? DateTime.Now : i.Fecha,
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
            var impresoras = await _context.Impresoras
                .OrderBy(i => i.Id)
                .ToListAsync();

            // ✅ Pasar la última exportación
            var ultima = _context.Configuraciones
                .FirstOrDefault(c => c.Clave == "UltimaExportacion");
            ViewBag.UltimaExportacion = ultima?.Valor;

            if (impresoras == null || !impresoras.Any())
            {
                // ⚡ Avisar que no hay registros
                ViewBag.SinRegistros = true;

                // ⚡ Generar lista vacía con 9 impresoras inicializadas
                var vacios = Enumerable.Range(0, 9)
                    .Select(i => new Impresora
                    {
                        Codigo = string.Empty,
                        InkCoreRemainingHours = 0,
                        Downtime = 0,
                        Additive = false,
                        Fecha = DateTime.Now.Date,
                        Hora = TimeSpan.Zero,
                        LocationExtru = string.Empty,
                        Tipo = string.Empty,
                        Status = string.Empty,
                        Comentario = string.Empty
                    })
                    .ToList();

                return View(vacios);
            }

            // ✅ Si sí hay registros, pasarlos tal cual
            ViewBag.SinRegistros = false;
            return View(impresoras);
        }


        //ACTUALIZAR TABLA DE INDEX

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
            foreach (var imp in impresoras)
            {
                // 👇 DEBUG
                Console.WriteLine(
                    $"ID={imp.Id} CODIGO={imp.Codigo} DOWNTIME RECIBIDO={imp.Downtime}");

                var dbImp = _context.Impresoras.FirstOrDefault(x => x.Id == imp.Id);

                if (dbImp != null)
                {
                    Console.WriteLine(
$"ANTES -> ID={dbImp.Id} DOWNTIME_BD={dbImp.Downtime}");

                    Console.WriteLine(
                        $"RECIBIDO -> ID={imp.Id} DOWNTIME_FORM={imp.Downtime}");

                    // Campos editables HIDDEN
                    dbImp.LocationExtru = imp.LocationExtru;
                    dbImp.Tipo = imp.Tipo;
                    dbImp.Status = imp.Status;
                    dbImp.Comentario = imp.Comentario;

                    // Campos editables desde STATUS
                    dbImp.Codigo = imp.Codigo;
                    dbImp.InkCoreRemainingHours = imp.InkCoreRemainingHours;
                    dbImp.Downtime = imp.Downtime;
                    Console.WriteLine(
    $"DESPUES -> ID={dbImp.Id} DOWNTIME_NUEVO={dbImp.Downtime}");
                    dbImp.Additive = imp.Additive;



                    if (imp.Downtime > 0)
                    {
                        RegistrarDowntimeHistorico(imp);

                        Console.WriteLine(
                            $"CODIGO={imp.Codigo} DOWNTIME HISTORIAL={imp.Downtime}");

                        dbImp.Downtime = 0;
                    }

                    // Fecha y hora automáticas
                    dbImp.Fecha = DateTime.Now.Date;
                    dbImp.Hora = DateTime.Now.TimeOfDay;
                }
                else
                {
                    _context.Impresoras.Add(new Impresora
                    {
                        Codigo = imp.Codigo,
                        InkCoreRemainingHours = imp.InkCoreRemainingHours,
                        Downtime = imp.Downtime,
                        Additive = imp.Additive,
                        Fecha = DateTime.Now.Date,
                        Hora = DateTime.Now.TimeOfDay,
                        Comentario = string.Empty,
                        Status = string.Empty,
                        LocationExtru = string.Empty,
                        Tipo = string.Empty
                    });
                }
            }

            _context.SaveChanges();

            ViewBag.Mensaje = true;
            return View("Status", _context.Impresoras.OrderBy(i => i.Id).ToList());
        }



        [HttpPost]
        public IActionResult GuardarTaller([Bind(Prefix = "impresoras")] List<Impresora> impresoras)
        {
            Console.WriteLine("===== ENTRO A GUARDAR TALLER =====");

            if (!ModelState.IsValid)
            {
                Console.WriteLine("MODELSTATE INVALIDO");
            }

            foreach (var imp in impresoras)
            {
                Console.WriteLine(
                    $"ID={imp.Id} CODIGO={imp.Codigo} DOWNTIME={imp.Downtime}");

                var dbImp = _context.Impresoras.FirstOrDefault(x => x.Id == imp.Id);

                if (dbImp != null)
                {
                    dbImp.Codigo = imp.Codigo;
                    dbImp.InkCoreRemainingHours = imp.InkCoreRemainingHours;
                    dbImp.Downtime = imp.Downtime;
                    dbImp.Comentario = imp.Comentario;
                    dbImp.Fecha = DateTime.Now.Date;
                    dbImp.Hora = DateTime.Now.TimeOfDay;

                    if (imp.Downtime > 0)
                    {
                        Console.WriteLine(
                            $"REGISTRANDO HISTORIAL -> {imp.Codigo} = {imp.Downtime}");

                        RegistrarDowntimeHistorico(imp);

                        // Reiniciar después de registrar
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