using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using System.Globalization;
using EXTRUDERNUCLEOS.Models;
using static System.Runtime.InteropServices.JavaScript.JSType;

using Microsoft.AspNetCore.SignalR;
using EXTRUDERNUCLEOS.Hubs; // 🔑 importa tu Hub

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllersWithViews()
    .AddViewLocalization()
    .AddDataAnnotationsLocalization();


builder.Services.AddDbContext<ApplicationDbContext>();

// builder.Services.AddHostedService<ExcelExportService>();

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddSignalR();

var app = builder.Build();

app.UseHttpsRedirection();

// Idiomas soportados
var supportedCultures = new[] { "es", "en", "ja" };
var localizationOptions = new RequestLocalizationOptions()
    .SetDefaultCulture("es")
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures);

// 🔑 habilitar parámetros ?culture=en&ui-culture=en
localizationOptions.RequestCultureProviders.Insert(0, new QueryStringRequestCultureProvider());

// 🔑 debe ir antes de Routing
app.UseRequestLocalization(localizationOptions);

app.UseRouting();
app.UseAuthorization();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
app.MapStaticAssets();


// 🔑 Aquí va MapHub, después de Build y antes de Run
app.MapHub<ExportHub>("/exportHub");



app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Login}/{id?}")
    .WithStaticAssets();

app.Run();