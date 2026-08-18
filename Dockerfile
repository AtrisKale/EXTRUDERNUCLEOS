# Fase base para ejecutar la aplicación
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

# Fase de compilación
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet build "./EXTRUDERNUCLEOS.csproj" -c Release -o /app/build

# --- AQUÍ ESTABA EL FALTANTE ---
# Fase de publicación (crea los binarios optimizados)
FROM build AS publish
RUN dotnet publish "./EXTRUDERNUCLEOS.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Fase final de producción
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Variables de entorno y punto de entrada (deben ir dentro de la fase final)
ENV CONNECTIONSTRINGS__DefaultConnection="Server=10.195.10.166,1433;Database=Mantenimiento;User Id= Manu; Password=2022.Tgram2;TrustServerCertificate=True;"
ENTRYPOINT ["dotnet", "EXTRUDERNUCLEOS.dll"]
