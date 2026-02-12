#!/usr/bin/env bash
set -euo pipefail

# HotBox bare-metal deployment script for Linux VPS
# Usage: sudo ./deploy/bare-metal-deploy.sh [install|update|uninstall]
#
# Prerequisites: .NET 8 runtime, PostgreSQL (or SQLite for simple setups)

APP_NAME="hotbox"
APP_USER="hotbox"
APP_DIR="/opt/hotbox"
SERVICE_FILE="/etc/systemd/system/hotbox.service"
NGINX_CONF="/etc/nginx/sites-available/hotbox"

print_usage() {
    echo "Usage: sudo $0 {install|update|uninstall}"
    echo ""
    echo "Commands:"
    echo "  install   - First-time setup (creates user, builds, installs service)"
    echo "  update    - Pull latest code, rebuild, and restart service"
    echo "  uninstall - Stop service and remove application files"
}

require_root() {
    if [ "$(id -u)" -ne 0 ]; then
        echo "Error: This script must be run as root (use sudo)"
        exit 1
    fi
}

cmd_install() {
    require_root

    REPO_DIR="${2:-$(pwd)}"

    echo "=== HotBox Bare-Metal Installation ==="
    echo ""

    # Create application user
    if ! id "$APP_USER" &>/dev/null; then
        echo "Creating application user '$APP_USER'..."
        useradd --system --shell /usr/sbin/nologin --home-dir "$APP_DIR" "$APP_USER"
    fi

    # Create application directory
    mkdir -p "$APP_DIR"

    # Build and publish
    echo "Building HotBox..."
    dotnet publish "$REPO_DIR/src/HotBox.Application/HotBox.Application.csproj" \
        -c Release \
        -o "$APP_DIR" \
        --nologo

    # Create default appsettings.Production.json if it doesn't exist
    if [ ! -f "$APP_DIR/appsettings.Production.json" ]; then
        echo "Creating production configuration..."
        cat > "$APP_DIR/appsettings.Production.json" << 'SETTINGS'
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "Database": {
    "Provider": "sqlite",
    "ConnectionString": "Data Source=/opt/hotbox/data/hotbox.db"
  }
}
SETTINGS
        mkdir -p "$APP_DIR/data"
    fi

    # Set ownership
    chown -R "$APP_USER:$APP_USER" "$APP_DIR"

    # Install systemd service
    echo "Installing systemd service..."
    cat > "$SERVICE_FILE" << 'SERVICE'
[Unit]
Description=HotBox Chat Server
After=network.target

[Service]
Type=notify
User=hotbox
Group=hotbox
WorkingDirectory=/opt/hotbox
ExecStart=/usr/bin/dotnet /opt/hotbox/HotBox.Application.dll
Restart=on-failure
RestartSec=5
TimeoutStartSec=30
TimeoutStopSec=15
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://localhost:5000
Environment=DOTNET_NOLOGO=1

# Hardening
NoNewPrivileges=true
ProtectSystem=strict
ProtectHome=true
ReadWritePaths=/opt/hotbox/data
PrivateTmp=true

[Install]
WantedBy=multi-user.target
SERVICE

    systemctl daemon-reload
    systemctl enable hotbox
    systemctl start hotbox

    echo ""
    echo "=== Installation Complete ==="
    echo "Service status: $(systemctl is-active hotbox)"
    echo "Listening on:   http://localhost:5000"
    echo ""
    echo "Next steps:"
    echo "  1. Configure a reverse proxy (nginx) — see docs/deployment/bare-metal.md"
    echo "  2. Edit $APP_DIR/appsettings.Production.json to switch to PostgreSQL"
    echo "  3. Set up TLS with certbot"
}

cmd_update() {
    require_root

    REPO_DIR="${2:-$(pwd)}"

    echo "Building HotBox..."
    dotnet publish "$REPO_DIR/src/HotBox.Application/HotBox.Application.csproj" \
        -c Release \
        -o "$APP_DIR" \
        --nologo

    chown -R "$APP_USER:$APP_USER" "$APP_DIR"

    echo "Restarting service..."
    systemctl restart hotbox

    echo "Update complete. Status: $(systemctl is-active hotbox)"
}

cmd_uninstall() {
    require_root

    echo "Stopping and disabling service..."
    systemctl stop hotbox 2>/dev/null || true
    systemctl disable hotbox 2>/dev/null || true
    rm -f "$SERVICE_FILE"
    systemctl daemon-reload

    echo ""
    echo "Service removed. Application files remain at $APP_DIR"
    echo "To fully remove: rm -rf $APP_DIR && userdel $APP_USER"
}

case "${1:-}" in
    install)   cmd_install "$@" ;;
    update)    cmd_update "$@" ;;
    uninstall) cmd_uninstall ;;
    *)         print_usage; exit 1 ;;
esac
