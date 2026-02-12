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

In production you'll want TLS termination in front of HotBox. Example Nginx config:

```nginx
server {
    listen 443 ssl http2;
    server_name chat.example.com;

    ssl_certificate     /etc/letsencrypt/live/chat.example.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/chat.example.com/privkey.pem;

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
    return 301 https://$server_name$request_uri;
}
```

The `Upgrade` / `Connection` headers are required for SignalR WebSocket connections.

Install and configure with:

```bash
sudo apt install nginx certbot python3-certbot-nginx
sudo certbot --nginx -d chat.example.com
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
