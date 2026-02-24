#!/usr/bin/env bash
set -euo pipefail

# HotBox Docker deployment script
# Usage: ./deploy/docker-deploy.sh [up|down|update|logs|status]

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(dirname "$SCRIPT_DIR")"
ENV_FILE="$PROJECT_DIR/.env"

print_usage() {
    echo "Usage: $0 {up|down|update|logs|status}"
    echo ""
    echo "Commands:"
    echo "  up      - Start HotBox (builds if needed)"
    echo "  down    - Stop and remove containers"
    echo "  update  - Pull latest code, rebuild, and restart"
    echo "  logs    - Tail container logs"
    echo "  status  - Show container status"
}

ensure_env() {
    if [ ! -f "$ENV_FILE" ]; then
        echo "Creating .env file with generated database password..."
        DB_PASSWORD=$(openssl rand -base64 24 | tr -d '/+=' | head -c 32)
        echo "DB_PASSWORD=$DB_PASSWORD" > "$ENV_FILE"
        chmod 600 "$ENV_FILE"
        echo "Generated .env at $ENV_FILE"
    fi
}

cmd_up() {
    ensure_env
    echo "Starting HotBox..."
    docker compose -f "$PROJECT_DIR/docker-compose.yml" up -d --build
    echo ""
    echo "HotBox is running at http://localhost:7200"
}

cmd_down() {
    echo "Stopping HotBox..."
    docker compose -f "$PROJECT_DIR/docker-compose.yml" down
}

cmd_update() {
    echo "Pulling latest changes..."
    git -C "$PROJECT_DIR" pull

    echo "Rebuilding and restarting..."
    docker compose -f "$PROJECT_DIR/docker-compose.yml" up -d --build

    echo "Cleaning up old images..."
    docker image prune -f

    echo ""
    echo "Update complete. HotBox is running at http://localhost:8080"
}

cmd_logs() {
    docker compose -f "$PROJECT_DIR/docker-compose.yml" logs -f --tail=100
}

cmd_status() {
    docker compose -f "$PROJECT_DIR/docker-compose.yml" ps
}

case "${1:-}" in
    up)     cmd_up ;;
    down)   cmd_down ;;
    update) cmd_update ;;
    logs)   cmd_logs ;;
    status) cmd_status ;;
    *)      print_usage; exit 1 ;;
esac
