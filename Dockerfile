# Esta fase se usa cuando se ejecuta desde VS en modo rápido (valor predeterminado para la configuración de depuración)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

# Esta fase se usa para compilar el proyecto de servicio
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

#Copiar el resto del codigo fuente
COPY . .

#Publicar en modo Release
RUN dotnet build "./EXTRUDERNUCLEOS.csproj" -c Release -o /app/build

# Esta fase se usa para publicar el proyecto de servicio que se copiará en la fase final.
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Esta fase se usa en producción o cuando se ejecuta desde VS en modo normal (valor predeterminado cuando no se usa la configuración de depuración)
ENV CONNECTIONSTRINGS__DefaultConnection="Server=10.195.10.166,1433;Database=Mantenimiento;User Id= Manu; Password=2022.Tgram2;TrustServerCertificate=True;"
ENTRYPOINT ["dotnet", "EXTRUDERNUCLEOS.dll"]