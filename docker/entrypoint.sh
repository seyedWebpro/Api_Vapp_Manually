#!/bin/bash
set -e

echo "🚀 Starting Vapp API..."

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

echo "⏳ Waiting for SQL Server to be ready..."
export PATH="$PATH:/opt/mssql-tools18/bin"

MAX_RETRIES=30
RETRY_COUNT=0

while [ $RETRY_COUNT -lt $MAX_RETRIES ]; do
    if sqlcmd -S sqlserver -U sa -P "$SA_PASSWORD" -C -Q "SELECT 1" &> /dev/null 2>&1; then
        echo "✅ SQL Server is ready!"
        break
    fi

    RETRY_COUNT=$((RETRY_COUNT + 1))
    if [ $RETRY_COUNT -lt $MAX_RETRIES ]; then
        echo "⏳ SQL Server is unavailable - sleeping (attempt $RETRY_COUNT/$MAX_RETRIES)"
        sleep 2
    else
        echo "⚠️  SQL Server connection timeout after $MAX_RETRIES attempts. Continuing anyway..."
    fi
done

echo "📦 Checking database..."
sqlcmd -S sqlserver -U sa -P "$SA_PASSWORD" -C -Q "IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'DbVapp') CREATE DATABASE DbVapp" 2>/dev/null || echo "⚠️ Could not create database (may already exist)"

echo "🎯 Starting application..."
echo "📌 Environment: ${ASPNETCORE_ENVIRONMENT:-Production}"
echo "📌 Database Provider: ${DatabaseProvider:-Docker}"
exec dotnet /app/Api_Vapp.dll
