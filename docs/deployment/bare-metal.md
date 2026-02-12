# Bare-Metal Deployment Guide

Deploy HotBox directly on a Linux VPS without Docker. This guide assumes Ubuntu/Debian, but the steps are similar for other distributions.

## Prerequisites

- Linux VPS with at least 1 GB RAM
- .NET 8 Runtime (ASP.NET Core)
- Git
- PostgreSQL 14+ (recommended) or SQLite

## 1. Install .NET 8 Runtime

```bash
# Ubuntu 22.04 / 24.04
sudo apt update
sudo apt install -y dotnet-sdk-8.0

# Verify
dotnet --version
```

For other distros, see the [Microsoft .NET install docs](https://learn.microsoft.com/en-us/dotnet/core/install/linux).

You only need the SDK on the build machine. Production servers can use just the runtime:

```bash
sudo apt install -y aspnetcore-runtime-8.0
```

## 2. Install PostgreSQL (Recommended)

```bash
sudo apt install -y postgresql postgresql-contrib

# Create database and user
sudo -u postgres psql << SQL
CREATE USER hotbox WITH PASSWORD 'your-secure-password';
CREATE DATABASE hotbox OWNER hotbox;
SQL
```

Skip this step if you want to use SQLite instead.

## 3. Clone and Build

```bash
git clone https://github.com/your-org/hotbox-chat.git /opt/hotbox-src
cd /opt/hotbox-src
```

### Automated Install

```bash
sudo ./deploy/bare-metal-deploy.sh install
```

This creates a `hotbox` system user, builds the app to `/opt/hotbox`, installs a systemd service, and starts it.

### Manual Install

If you prefer to do it step by step:

```bash
# Build
dotnet publish src/HotBox.Application/HotBox.Application.csproj \
    -c Release -o /opt/hotbox

# Create system user
sudo useradd --system --shell /usr/sbin/nologin --home-dir /opt/hotbox hotbox
sudo chown -R hotbox:hotbox /opt/hotbox
```

## 4. Configure

Edit `/opt/hotbox/appsettings.Production.json`:

### With PostgreSQL

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "Database": {
    "Provider": "postgresql",
    "ConnectionString": "Host=localhost;Port=5432;Database=hotbox;Username=hotbox;Password=your-secure-password"
  }
}
```

### With SQLite

```json
{
  "Database": {
    "Provider": "sqlite",
    "ConnectionString": "Data Source=/opt/hotbox/data/hotbox.db"
  }
}
```

Make sure the data directory exists and is writable:

```bash
sudo mkdir -p /opt/hotbox/data
sudo chown hotbox:hotbox /opt/hotbox/data
```

## 5. Systemd Service

If you used the automated install, this is already done. Otherwise, create `/etc/systemd/system/hotbox.service`:

```ini
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
```

Enable and start:

```bash
sudo systemctl daemon-reload
sudo systemctl enable hotbox
sudo systemctl start hotbox

# Check status
sudo systemctl status hotbox
sudo journalctl -u hotbox -f
```

## 6. Nginx Reverse Proxy + TLS

### Step 1: HTTP-Only Config (for Certbot)

Install nginx and certbot:

```bash
sudo apt install -y nginx certbot
```

Create `/etc/nginx/sites-available/hotbox`:

```nginx
server {
    listen 80;
    server_name chat.example.com;

    # Certbot challenge directory
    location /.well-known/acme-challenge/ {
        root /var/www/certbot;
    }

    # Proxy everything else to HotBox while waiting for certs
    location / {
        proxy_pass http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

Enable the site and run certbot:

```bash
sudo ln -s /etc/nginx/sites-available/hotbox /etc/nginx/sites-enabled/
sudo rm -f /etc/nginx/sites-enabled/default
sudo mkdir -p /var/www/certbot
sudo nginx -t && sudo systemctl reload nginx

# Obtain the certificate
sudo certbot certonly --webroot -w /var/www/certbot -d chat.example.com
```

### Step 2: HTTPS Config (after first cert)

Once certbot succeeds, replace `/etc/nginx/sites-available/hotbox` with the full config:

```nginx
server {
    listen 443 ssl http2;
    server_name chat.example.com;

    ssl_certificate     /etc/letsencrypt/live/chat.example.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/chat.example.com/privkey.pem;

    # Mozilla intermediate TLS settings
    ssl_protocols TLSv1.2 TLSv1.3;
    ssl_ciphers ECDHE-ECDSA-AES128-GCM-SHA256:ECDHE-RSA-AES128-GCM-SHA256:ECDHE-ECDSA-AES256-GCM-SHA384:ECDHE-RSA-AES256-GCM-SHA384;
    ssl_prefer_server_ciphers off;

    # HSTS (optional — uncomment after confirming TLS works)
    # add_header Strict-Transport-Security "max-age=63072000" always;

    location / {
        proxy_pass http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}

server {
    listen 80;
    server_name chat.example.com;

    # Continue serving certbot renewals over HTTP
    location /.well-known/acme-challenge/ {
        root /var/www/certbot;
    }

    location / {
        return 301 https://$server_name$request_uri;
    }
}
```

Reload nginx:

```bash
sudo nginx -t && sudo systemctl reload nginx
```

The `Upgrade` / `Connection` headers are required for SignalR WebSocket connections.

### Auto-Renewal

Certbot installs a systemd timer or cron job automatically. Verify it works:

```bash
sudo certbot renew --dry-run
```

## 8. Firewall

```bash
sudo ufw allow 22/tcp    # SSH
sudo ufw allow 80/tcp    # HTTP (redirect)
sudo ufw allow 443/tcp   # HTTPS
sudo ufw enable
```

## Updating

```bash
cd /opt/hotbox-src
git pull
sudo ./deploy/bare-metal-deploy.sh update
```

Or manually:

```bash
cd /opt/hotbox-src
git pull
dotnet publish src/HotBox.Application/HotBox.Application.csproj -c Release -o /opt/hotbox
sudo systemctl restart hotbox
```

## Backups

### PostgreSQL

```bash
# Dump
sudo -u hotbox pg_dump hotbox > /opt/hotbox/backups/hotbox_$(date +%Y%m%d).sql

# Automate with cron
echo "0 3 * * * hotbox pg_dump hotbox > /opt/hotbox/backups/hotbox_\$(date +\%Y\%m\%d).sql" | sudo tee /etc/cron.d/hotbox-backup
```

### SQLite

```bash
cp /opt/hotbox/data/hotbox.db /opt/hotbox/backups/hotbox_$(date +%Y%m%d).db
```

## Troubleshooting

### App won't start

```bash
# Check logs
sudo journalctl -u hotbox -n 50 --no-pager

# Test manually
sudo -u hotbox dotnet /opt/hotbox/HotBox.Application.dll
```

### 502 Bad Gateway from Nginx

- Verify HotBox is running: `systemctl is-active hotbox`
- Verify it's listening: `ss -tlnp | grep 5000`
- Check Nginx error log: `tail /var/log/nginx/error.log`

### Database connection refused

- PostgreSQL running? `systemctl status postgresql`
- Can connect? `sudo -u hotbox psql -h localhost -U hotbox hotbox`
- Check `pg_hba.conf` allows local connections with password auth
