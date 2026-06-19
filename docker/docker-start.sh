#!/bin/bash
# ===========================================
# Vapp API - Docker Manager
# اجرا از روت پروژه: ./docker/docker-start.sh {dev|sql|local|prod|...}
# ===========================================

set -e

GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
CYAN='\033[0;36m'
NC='\033[0m'

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
cd "$ROOT_DIR"

COMPOSE_FILE="$SCRIPT_DIR/docker-compose.yml"
DEV_SQL_FILE="$SCRIPT_DIR/docker-compose.dev-sqlserver.yml"
LOCAL_FILE="$SCRIPT_DIR/docker-compose.local.yml"
PROD_FILE="$SCRIPT_DIR/docker-compose.production.yml"
ENV_FILE="$SCRIPT_DIR/.env"

compose() {
    if [ -f "$ENV_FILE" ]; then
        docker compose --env-file "$ENV_FILE" "$@"
    else
        docker compose "$@"
    fi
}

API_PORT="5054"
SQL_PORT="1436"
SA_PASSWORD="Vapp@Secure2025!"

echo -e "${CYAN}============================================"
echo -e "   Vapp API - Docker (Local)"
echo -e "============================================${NC}\n"

ACTION=${1:-dev}

start_development() {
    echo -e "${GREEN}[DEV] Starting SQL Server + API...${NC}"
    compose -f "$COMPOSE_FILE" up -d
    echo -e "\n${YELLOW}[INFO] Services starting...${NC}"
    echo -e "${YELLOW}[INFO] API:     http://localhost:${API_PORT}${NC}"
    echo -e "${YELLOW}[INFO] Swagger: http://localhost:${API_PORT}/swagger${NC}"
    echo -e "${YELLOW}[INFO] SQL Server: localhost:${SQL_PORT} (user: sa, db: DbVapp)${NC}"
    echo -e "\n${YELLOW}[INFO] Waiting for services to be healthy...${NC}"
    sleep 15
    compose -f "$COMPOSE_FILE" ps
}

start_sql_only() {
    echo -e "${GREEN}[SQL] Starting SQL Server only (API on host with dotnet run)...${NC}"
    compose -f "$DEV_SQL_FILE" up -d
    echo -e "${YELLOW}[INFO] SQL Server: localhost:${SQL_PORT} (user: sa)${NC}"
    echo -e "${YELLOW}[INFO] Run API: dotnet run --launch-profile \"http (SQL in Docker, API on host)\"${NC}"
    compose -f "$DEV_SQL_FILE" ps
}

start_local_only() {
    echo -e "${GREEN}[LOCAL] Starting API only (SQL on host via host.docker.internal)...${NC}"
    compose -f "$LOCAL_FILE" up -d
    echo -e "${YELLOW}[INFO] API: http://localhost:${API_PORT}${NC}"
    compose -f "$LOCAL_FILE" ps
}

stop_services() {
    echo -e "${YELLOW}[DOWN] Stopping services...${NC}"
    compose -f "$COMPOSE_FILE" down
    echo -e "${GREEN}[INFO] Stopped.${NC}"
}

stop_sql_only() {
    compose -f "$DEV_SQL_FILE" down
    echo -e "${GREEN}[INFO] Dev SQL Server stopped.${NC}"
}

stop_local() {
    compose -f "$LOCAL_FILE" down
    echo -e "${GREEN}[INFO] Local API stopped.${NC}"
}

build_images() {
    echo -e "${GREEN}[BUILD] Building Docker images...${NC}"
    compose -f "$COMPOSE_FILE" build --no-cache
    echo -e "${GREEN}[INFO] Build completed.${NC}"
}

show_logs() {
    echo -e "${YELLOW}[LOGS] (Ctrl+C to exit)${NC}"
    compose -f "$COMPOSE_FILE" logs -f
}

clean_all() {
    echo -e "${RED}[CLEAN] Remove containers, volumes, images?${NC}"
    read -p "Continue? (y/N) " confirm
    if [[ $confirm == [yY] ]]; then
        compose -f "$COMPOSE_FILE" down -v --rmi local --remove-orphans
        echo -e "${GREEN}[INFO] Cleanup done.${NC}"
    else
        echo -e "${YELLOW}Cancelled.${NC}"
    fi
}

wait_for_db() {
    echo -e "${YELLOW}[WAIT] Waiting for SQL Server...${NC}"
    until compose -f "$DEV_SQL_FILE" exec -T sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$SA_PASSWORD" -C -Q "SELECT 1" &>/dev/null; do
        echo -e "${YELLOW}[WAIT] SQL Server starting...${NC}"
        sleep 5
    done
    echo -e "${GREEN}[INFO] SQL Server is ready.${NC}"
}

migrate_help() {
    echo -e "${YELLOW}[MIGRATE] Run migrations from host:${NC}"
    echo -e "  cd $ROOT_DIR"
    echo -e "  dotnet ef database update --connection \"Server=localhost,${SQL_PORT};Database=DbVapp;User Id=sa;Password=${SA_PASSWORD};TrustServerCertificate=True;Encrypt=False;\""
}

case $ACTION in
    dev)
        start_development
        ;;
    sql)
        start_sql_only
        ;;
    local)
        start_local_only
        ;;
    prod)
        echo -e "${GREEN}[PROD] Starting production stack...${NC}"
        compose -f "$PROD_FILE" up -d
        compose -f "$PROD_FILE" ps
        ;;
    build)
        build_images
        ;;
    down)
        stop_services
        ;;
    down-sql)
        stop_sql_only
        ;;
    down-local)
        stop_local
        ;;
    logs)
        show_logs
        ;;
    clean)
        clean_all
        ;;
    wait-db)
        wait_for_db
        ;;
    migrate)
        migrate_help
        ;;
    *)
        echo "Usage: $0 {dev|sql|local|prod|build|down|down-sql|down-local|logs|clean|wait-db|migrate}"
        exit 1
        ;;
esac

echo -e "\n${CYAN}============================================${NC}"
