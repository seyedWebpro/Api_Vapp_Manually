#!/bin/bash
set -e

echo "Starting Vapp API..."

if [ -z "$SA_PASSWORD" ]; then
    SA_PASSWORD="Vapp@Secure2025!"
fi

export ConnectionStrings__DockerConnection="Server=sqlserver;Database=DbVapp;User Id=sa;Password=${SA_PASSWORD};TrustServerCertificate=True;Encrypt=False;MultipleActiveResultSets=true;Max Pool Size=100;Min Pool Size=5;Connection Timeout=30;Pooling=true"
export ConnectionStrings__LocalConnection="Server=sqlserver;Database=DbVapp;User Id=sa;Password=${SA_PASSWORD};TrustServerCertificate=True;Encrypt=False;MultipleActiveResultSets=true;Max Pool Size=100;Min Pool Size=5;Connection Timeout=30;Pooling=true"
export DatabaseProvider="Docker"

if [ -f "/run/secrets/mssql_sa_password" ]; then
    export SA_PASSWORD=$(cat /run/secrets/mssql_sa_password)
    export ConnectionStrings__DockerConnection="Server=sqlserver;Database=DbVapp;User Id=sa;Password=${SA_PASSWORD};TrustServerCertificate=True;Encrypt=False;MultipleActiveResultSets=true;Max Pool Size=100;Min Pool Size=5;Connection Timeout=30;Pooling=true"
    export ConnectionStrings__LocalConnection="Server=sqlserver;Database=DbVapp;User Id=sa;Password=${SA_PASSWORD};TrustServerCertificate=True;Encrypt=False;MultipleActiveResultSets=true;Max Pool Size=100;Min Pool Size=5;Connection Timeout=30;Pooling=true"
fi

echo "Waiting for SQL Server port..."
MAX_RETRIES=60
RETRY_COUNT=0
while [ $RETRY_COUNT -lt $MAX_RETRIES ]; do
    if (echo > /dev/tcp/sqlserver/1433) 2>/dev/null; then
        echo "SQL Server port is open."
        break
    fi
    RETRY_COUNT=$((RETRY_COUNT + 1))
    sleep 2
done

if [ $RETRY_COUNT -ge $MAX_RETRIES ]; then
    echo "WARN: SQL Server port timeout — starting app anyway (EF will retry)."
fi

echo "Starting application..."
echo "Environment: ${ASPNETCORE_ENVIRONMENT:-Production}"
exec dotnet /app/Api_Vapp.dll
