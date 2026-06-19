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

RUN apt-get update && apt-get install -y curl gnupg && \
    curl https://packages.microsoft.com/keys/microsoft.asc | apt-key add - && \
    curl https://packages.microsoft.com/config/debian/11/prod.list > /etc/apt/sources.list.d/mssql-release.list && \
    apt-get update && \
    ACCEPT_EULA=Y apt-get install -y mssql-tools18 unixodbc-dev && \
    echo 'export PATH="$PATH:/opt/mssql-tools18/bin"' >> ~/.bashrc && \
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
ENV PATH="$PATH:/opt/mssql-tools18/bin"

ENTRYPOINT ["/bin/bash", "-c", "if [ -f /app/entrypoint.sh ]; then exec /app/entrypoint.sh; else exec dotnet /app/Api_Vapp.dll; fi"]
