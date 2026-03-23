# HotBox v2 — Bare-Metal Deployment

**Version**: 2.0
**Date**: 2026-03-22
**Status**: Draft

---

## Overview

This guide covers deploying HotBox v2 directly on a Linux VPS without Docker. Suitable for single-server deployments where you want full control over the runtime environment.

---

## Prerequisites

| Component | Minimum Version | Notes |
|-----------|----------------|-------|
| Linux | Ubuntu 22.04+ / Debian 12+ | Any systemd-based distro |
| .NET Runtime | ASP.NET Core 9.0 | Runtime only, not the full SDK |
| PostgreSQL | 16+ | Required |
| Redis | 7+ | Required for caching and SignalR backplane |
| Nginx or Caddy | Latest | Reverse proxy with TLS |

---

## Step 1: Install .NET 9 Runtime

```bash
# Ubuntu / Debian
wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --channel 9.0 --runtime aspnetcore --install-dir /usr/share/dotnet
ln -sf /usr/share/dotnet/dotnet /usr/bin/dotnet

# Verify
dotnet --list-runtimes
# Should show: Microsoft.AspNetCore.App 9.0.x
```

---

## Step 2: Install PostgreSQL

```bash
sudo apt install -y postgresql-16

# Create database and user
sudo -u postgres psql <<'SQL'
CREATE USER hotbox WITH PASSWORD 'your-secure-password';
CREATE DATABASE hotbox OWNER hotbox;
GRANT ALL PRIVILEGES ON DATABASE hotbox TO hotbox;
SQL

# Verify connection
psql -h localhost -U hotbox -d hotbox -c "SELECT 1;"
```

---

## Step 3: Install Redis

```bash
sudo apt install -y redis-server

# Enable and start
sudo systemctl enable redis-server
sudo systemctl start redis-server

# Verify
redis-cli ping
# Should return: PONG
```

Optional: configure Redis password in `/etc/redis/redis.conf`:
```
requirepass your-redis-password
```

---

## Step 4: Build and Deploy HotBox

### Option A: Build from Source

```bash
# Install .NET SDK (build machine only)
./dotnet-install.sh --channel 9.0

# Clone and build
git clone https://github.com/cpike5/hotbox-chat.git
cd hotbox-chat
dotnet publish src/HotBox.Application/HotBox.Application.csproj \
    -c Release -o /opt/hotbox
```

### Option B: Download Pre-built Release

```bash
mkdir -p /opt/hotbox
curl -LO https://github.com/cpike5/hotbox-chat/releases/latest/download/hotbox-linux-x64.tar.gz
tar xzf hotbox-linux-x64.tar.gz -C /opt/hotbox
```

---

## Step 5: Configure

Create `/opt/hotbox/appsettings.Production.json`:

```json
{
  "Server": {
    "ServerName": "My HotBox",
    "RegistrationMode": "Open"
  },
  "ConnectionStrings": {
    "Postgres": "Host=localhost;Port=5432;Database=hotbox;Username=hotbox;Password=your-secure-password",
    "Redis": "localhost:6379"
  },
  "Jwt": {
    "Secret": "your-jwt-secret-at-least-32-characters-long"
  },
  "AdminSeed": {
    "Email": "admin@example.com",
    "Password": "YourSecureAdminPassword123!",
    "DisplayName": "Admin"
  },
  "Observability": {
    "LogLevel": "Information"
  }
}
```

If Redis has a password:
```json
{
  "ConnectionStrings": {
    "Redis": "localhost:6379,password=your-redis-password"
  }
}
```

See `docs/v2/configuration-reference.md` for all available settings.

---

## Step 6: Create Service User

```bash
sudo useradd -r -s /bin/false hotbox
sudo chown -R hotbox:hotbox /opt/hotbox
```

---

## Step 7: Create systemd Service

Create `/etc/systemd/system/hotbox.service`:

```ini
[Unit]
Description=HotBox Chat Server
After=network.target postgresql.service redis-server.service
Requires=postgresql.service redis-server.service

[Service]
Type=notify
User=hotbox
Group=hotbox
WorkingDirectory=/opt/hotbox
ExecStart=/usr/bin/dotnet /opt/hotbox/HotBox.Application.dll
Restart=on-failure
RestartSec=10
TimeoutStartSec=60

# Environment
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://127.0.0.1:5000
Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false

# Security hardening
ProtectSystem=strict
ProtectHome=true
NoNewPrivileges=true
PrivateTmp=true
PrivateDevices=true
ProtectKernelTunables=true
ProtectKernelModules=true
ProtectControlGroups=true
RestrictSUIDSGID=true
RestrictNamespaces=true
LockPersonality=true
ReadWritePaths=/opt/hotbox

# Resource limits
LimitNOFILE=65536

[Install]
WantedBy=multi-user.target
```

```bash
sudo systemctl daemon-reload
sudo systemctl enable hotbox
sudo systemctl start hotbox

# Verify
sudo systemctl status hotbox
curl http://127.0.0.1:5000/health
```

---

## Step 8: Configure Nginx Reverse Proxy

```bash
sudo apt install -y nginx
```

Create `/etc/nginx/sites-available/hotbox`:

```nginx
server {
    listen 80;
    server_name chat.example.com;

    location / {
        proxy_pass http://127.0.0.1:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;

        # SignalR / WebSocket timeouts
        proxy_read_timeout 86400s;
        proxy_send_timeout 86400s;

        # Buffering (important for SignalR)
        proxy_buffering off;
    }
}
```

```bash
sudo ln -s /etc/nginx/sites-available/hotbox /etc/nginx/sites-enabled/
sudo nginx -t
sudo systemctl reload nginx
```

---

## Step 9: TLS with Let's Encrypt

```bash
sudo apt install -y certbot python3-certbot-nginx
sudo certbot --nginx -d chat.example.com

# Auto-renewal (certbot installs a timer by default)
sudo certbot renew --dry-run
```

---

## Step 10: Firewall

```bash
sudo ufw allow 22/tcp     # SSH
sudo ufw allow 80/tcp     # HTTP (redirects to HTTPS)
sudo ufw allow 443/tcp    # HTTPS
sudo ufw enable
```

If using a TURN server for voice:
```bash
sudo ufw allow 3478/tcp   # TURN
sudo ufw allow 3478/udp   # TURN
sudo ufw allow 49152:65535/udp  # TURN relay range
```

---

## Updating

```bash
# Stop the service
sudo systemctl stop hotbox

# Build or download new version
cd /path/to/hotbox-chat
git pull
dotnet publish src/HotBox.Application/HotBox.Application.csproj \
    -c Release -o /opt/hotbox

# Fix permissions
sudo chown -R hotbox:hotbox /opt/hotbox

# Restart
sudo systemctl start hotbox

# Verify
sudo systemctl status hotbox
curl http://127.0.0.1:5000/health
```

EF Core migrations run automatically on startup.

---

## Backups

### PostgreSQL

```bash
# Dump
pg_dump -U hotbox hotbox > /var/backups/hotbox/db_$(date +%Y%m%d).sql

# Restore
psql -U hotbox hotbox < /var/backups/hotbox/db_20260322.sql
```

### Automated Backup (Cron)

```bash
sudo mkdir -p /var/backups/hotbox
sudo chown hotbox:hotbox /var/backups/hotbox

# Add to crontab (daily at 3 AM)
echo "0 3 * * * pg_dump -U hotbox hotbox | gzip > /var/backups/hotbox/db_\$(date +\%Y\%m\%d).sql.gz" | sudo tee /etc/cron.d/hotbox-backup
```

### Redis

Redis is configured with RDB snapshots by default. The dump file is at `/var/lib/redis/dump.rdb`.

---

## Log Management

### Journald

```bash
# View logs
sudo journalctl -u hotbox -f

# Last 100 lines
sudo journalctl -u hotbox -n 100 --no-pager

# Since last hour
sudo journalctl -u hotbox --since "1 hour ago"
```

### Optional: Seq

Install Seq for structured log viewing:

```bash
docker run -d --name seq \
    -e ACCEPT_EULA=Y \
    -p 5341:5341 \
    -p 7201:80 \
    -v seq-data:/data \
    datalust/seq:latest
```

Add to `appsettings.Production.json`:
```json
{
  "Observability": {
    "SeqUrl": "http://localhost:5341"
  }
}
```

---

## Monitoring

### Health Check

```bash
# Simple check
curl -f http://127.0.0.1:5000/health || echo "UNHEALTHY"

# Detailed (if detailed health check output is enabled)
curl http://127.0.0.1:5000/health | jq .
```

### Alerting

Add a simple cron monitor:

```bash
# Check every 5 minutes, restart if unhealthy
echo "*/5 * * * * curl -sf http://127.0.0.1:5000/health > /dev/null || systemctl restart hotbox" | sudo tee /etc/cron.d/hotbox-monitor
```

For production, integrate with your monitoring stack (Uptime Kuma, Grafana, etc.).

---

## Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| Service won't start | Missing .NET runtime | Install ASP.NET Core 9.0 runtime |
| Database connection refused | PostgreSQL not running | `systemctl start postgresql` |
| Redis connection refused | Redis not running | `systemctl start redis-server` |
| 502 Bad Gateway | App not listening | Check `systemctl status hotbox` and logs |
| SignalR disconnects | Nginx timeout | Add `proxy_read_timeout 86400s` |
| Permission denied | Wrong file ownership | `chown -R hotbox:hotbox /opt/hotbox` |
| Port already in use | Another process on 5000 | `ss -tlnp | grep 5000` to identify |
