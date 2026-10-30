# syntax=docker/dockerfile:1
# =====================================================================
#  ClimaPlus – Imagen de la Web API (ASP.NET Core 8) · Fase 9
#  Etapa 1 compila con el SDK; etapa 2 ejecuta solo con el runtime, como usuario sin privilegios.
#  Construir:  docker build -t climaplus-api ./backend
# =====================================================================

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Primero solo el .csproj: la restauración queda en caché mientras no cambien los paquetes.
COPY ClimaPlus.Api/ClimaPlus.Api.csproj ClimaPlus.Api/
RUN dotnet restore ClimaPlus.Api/ClimaPlus.Api.csproj

COPY ClimaPlus.Api/ ClimaPlus.Api/
RUN dotnet publish ClimaPlus.Api/ClimaPlus.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_NOLOGO=true

COPY --from=build /app/publish .

# OWASP A05: la imagen oficial trae el usuario "app" (UID 1654) sin privilegios.
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "ClimaPlus.Api.dll"]
