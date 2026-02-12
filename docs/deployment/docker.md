# Docker Deployment Guide

Deploy HotBox using Docker Compose. This is the recommended approach — it handles the database, networking, and restarts automatically.

## Prerequisites

- Docker Engine 24+ and Docker Compose v2
- Git (to clone the repo)
- A server with at least 1 GB RAM

## Quick Start

```bash
git clone https://github.com/your-org/hotbox-chat.git
cd hotbox-chat
./deploy/docker-deploy.sh up
```

HotBox will be available at `http://localhost:8080`.

The script generates a `.env` file with a random database password on first run.

## What Gets Created

| Container    | Purpose                     | Port |
|--------------|-----------------------------|------|
| `hotbox`     | ASP.NET Core app server     | 8080 |
| `hotbox-db`  | PostgreSQL 16               | —    |

PostgreSQL data is persisted in a Docker volume (`pgdata`).

## Configuration

### Environment Variables

Override settings via the `.env` file or by editing `docker-compose.yml`:

| Variable                     | Default       | Description              |
|------------------------------|---------------|--------------------------|
| `DB_PASSWORD`                | `changeme`    | PostgreSQL password      |
| `Database__Provider`         | `postgresql`  | Database provider        |
| `Database__ConnectionString` | *(see compose)* | Full connection string |

### Using MySQL/MariaDB Instead

Replace the `db` service in `docker-compose.yml`:

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
    build: .
    container_name: hotbox
    restart: unless-stopped
    ports:
      - "8080:8080"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - Database__Provider=sqlite
      - Database__ConnectionString=Data Source=/app/data/hotbox.db
    volumes:
      - hotbox-data:/app/data

volumes:
  hotbox-data:
```

Remove the `db` service entirely.

## Management Commands

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
        proxy_pass http://localhost:8080;
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
        proxy_pass http://localhost:8080;
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

## Updating

```bash
cd hotbox-chat
./deploy/docker-deploy.sh update
```

This pulls the latest code, rebuilds the container, and restarts with zero-downtime for the database (the `db` container is not recreated unless its config changes).
