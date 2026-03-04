# Docker Deployment Guide

Deploy HotBox using Docker Compose. This is the recommended approach — it handles the database, networking, and restarts automatically.

## Prerequisites

- Docker Engine 24+ and Docker Compose v2
- A server with at least 1 GB RAM

## Production Deployment (Pre-built Image)

The fastest way to deploy. Uses the pre-built image from GitHub Container Registry — no need to clone the repo or build anything.

### 1. Create a project directory

```bash
mkdir hotbox && cd hotbox
```

### 2. Download the compose file and env template

```bash
curl -LO https://raw.githubusercontent.com/cpike5/hotbox-chat/main/docker-compose.prod.yml
curl -LO https://raw.githubusercontent.com/cpike5/hotbox-chat/main/.env.example
cp .env.example .env
```

### 3. Configure secrets

Edit `.env` with real values:

```bash
# REQUIRED — generate with: openssl rand -base64 48
DB_PASSWORD=<strong-random-password>
JWT_SECRET=<random-string-at-least-32-characters>

# Admin account created on first startup
ADMIN_EMAIL=admin@example.com
ADMIN_PASSWORD=<strong-admin-password>
ADMIN_DISPLAY_NAME=Admin

# Optional port overrides (defaults: 7200 for app, 7201 for Seq)
# HOTBOX_PORT=7200
# SEQ_PORT=7201
```

### 4. Start

```bash
docker compose -f docker-compose.prod.yml up -d
```

### 5. Verify

```bash
# Check containers
docker compose -f docker-compose.prod.yml ps

# Check health
curl http://localhost:7200/health
```

**Access:**
- HotBox: `http://localhost:7200`
- Seq (logs): `http://localhost:7201`

### Updating

Pull the latest image and restart:

```bash
docker compose -f docker-compose.prod.yml pull
docker compose -f docker-compose.prod.yml up -d
docker image prune -f
```

To pin a specific version instead of `latest`, edit `docker-compose.prod.yml`:

```yaml
image: ghcr.io/cpike5/hotbox-chat:0.4.0
```

---

## Build from Source

If you prefer to build the image yourself (for development or customization):

```bash
git clone https://github.com/cpike5/hotbox-chat.git
cd hotbox-chat
cp .env.example .env
# Edit .env with your secrets
./deploy/docker-deploy.sh up
```

HotBox will be available at `http://localhost:7200`.

The deploy script generates a `.env` file with a random database password on first run.

### Management Commands

```bash
# Start / rebuild
./deploy/docker-deploy.sh up

# Stop
./deploy/docker-deploy.sh down

# Pull latest code, rebuild, and restart
./deploy/docker-deploy.sh update

# Tail logs
./deploy/docker-deploy.sh logs

# Check status
./deploy/docker-deploy.sh status
```

---

## What Gets Created

| Container    | Purpose                     | Default Port |
|--------------|-----------------------------|--------------|
| `hotbox`     | ASP.NET Core app server     | 7200         |
| `hotbox-db`  | PostgreSQL 16               | — (internal) |
| `hotbox-seq` | Seq log aggregation         | 7201         |

PostgreSQL data is persisted in a Docker volume (`pgdata`). Seq data in `seqdata`.

## Configuration

### Environment Variables

Override settings via the `.env` file:

| Variable               | Default                | Description                                  |
|------------------------|------------------------|----------------------------------------------|
| `DB_PASSWORD`          | *(required)*           | PostgreSQL password                          |
| `JWT_SECRET`           | *(required)*           | JWT signing key (min 32 chars)               |
| `ADMIN_EMAIL`          | `admin@hotbox.local`   | Initial admin account email                  |
| `ADMIN_PASSWORD`       | `Admin123!`            | Initial admin account password               |
| `ADMIN_DISPLAY_NAME`   | `Admin`                | Initial admin display name                   |
| `HOTBOX_PORT`          | `7200`                 | Host port for the app                        |
| `SEQ_PORT`             | `7201`                 | Host port for Seq web UI                     |
| `APM_OTLP_ENDPOINT`   | *(empty)*              | OpenTelemetry OTLP endpoint (optional)       |
| `APM_API_KEY`          | *(empty)*              | OTLP API key (optional)                      |
| `ELASTICSEARCH_URL`    | *(empty)*              | Elasticsearch URL for log shipping (optional)|
| `ELASTICSEARCH_API_KEY`| *(empty)*              | Elasticsearch API key (optional)             |

For the full list of application settings (OAuth, WebRTC TURN servers, search tuning, etc.), see the [Configuration Reference](../architecture/configuration-reference.md).

### Using MySQL/MariaDB Instead

Replace the `db` service in your compose file:

```yaml
services:
  db:
    image: mariadb:11
    container_name: hotbox-db
    restart: unless-stopped
    environment:
      MARIADB_DATABASE: hotbox
      MARIADB_USER: hotbox
      MARIADB_PASSWORD: ${DB_PASSWORD:-changeme}
      MARIADB_ROOT_PASSWORD: ${DB_ROOT_PASSWORD:-changeme}
    volumes:
      - dbdata:/var/lib/mysql
    healthcheck:
      test: ["CMD", "healthcheck.sh", "--connect"]
      interval: 5s
      timeout: 3s
      retries: 5
```

And update the `hotbox` service environment:

```yaml
environment:
  - Database__Provider=mariadb
  - Database__ConnectionString=Server=db;Port=3306;Database=hotbox;User=hotbox;Password=${DB_PASSWORD:-changeme}
```

### Using SQLite (Simplest)

For a single-file database with no separate container:

```yaml
services:
  hotbox:
    image: ghcr.io/cpike5/hotbox-chat:latest
    container_name: hotbox
    restart: unless-stopped
    ports:
      - "7200:8080"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - Database__Provider=sqlite
      - Database__ConnectionString=Data Source=/app/data/hotbox.db
      - Jwt__Secret=${JWT_SECRET:?Set JWT_SECRET in .env}
    volumes:
      - hotbox-data:/app/data

volumes:
  hotbox-data:
```

Remove the `db` service entirely.

## Reverse Proxy with Nginx

In production you'll want TLS termination in front of HotBox. This is a two-step process: start with HTTP to obtain your first certificate, then switch to the full HTTPS config.

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
        proxy_pass http://localhost:7200;
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
        proxy_pass http://localhost:7200;
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

## Backups

### PostgreSQL

```bash
# Dump
docker exec hotbox-db pg_dump -U hotbox hotbox > backup_$(date +%Y%m%d).sql

# Restore
cat backup.sql | docker exec -i hotbox-db psql -U hotbox hotbox
```

### SQLite

```bash
# Just copy the volume file
docker cp hotbox:/app/data/hotbox.db ./hotbox_backup_$(date +%Y%m%d).db
```
