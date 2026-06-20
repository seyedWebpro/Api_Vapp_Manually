# ===========================================
# Vapp API - Multi-stage Docker Build
# Build context: project root (..)
# ===========================================

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["Api_Vapp.sln", "./"]
COPY ["Api_Vapp.csproj", "./"]

RUN dotnet restore "Api_Vapp.sln"

COPY . .

RUN dotnet build "Api_Vapp.sln" -c Release -o /app/build --no-restore

FROM build AS publish
RUN dotnet publish "Api_Vapp.csproj" -c Release -o /app/publish /p:UseAppHost=false --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# sqlcmd حذف شد — برخی سرورها به packages.microsoft.com دسترسی ندارند.
# آماده‌شدن SQL با depends_on + healthcheck و migrate در Program.cs انجام می‌شود.
RUN apt-get update && apt-get install -y --no-install-recommends curl && \
    rm -rf /var/lib/apt/lists/*

COPY --from=publish /app/publish .

COPY --from=build ["/src/appsettings.json", "/app/appsettings.json"]
COPY --from=build ["/src/appsettings.Docker.json", "/app/appsettings.Docker.json"]

RUN mkdir -p /app/wwwroot/uploads && \
    mkdir -p /app/log && \
    chmod -R 755 /app/wwwroot/uploads && \
    chmod -R 755 /app/log

COPY --from=build ["/src/docker/entrypoint.sh", "/app/entrypoint.sh"]
RUN chmod +x /app/entrypoint.sh

EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true

ENTRYPOINT ["/bin/bash", "-c", "if [ -f /app/entrypoint.sh ]; then exec /app/entrypoint.sh; else exec dotnet /app/Api_Vapp.dll; fi"]
