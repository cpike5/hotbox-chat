# HotBox v2 — Docker Deployment

**Version**: 2.0
**Date**: 2026-03-22
**Status**: Draft

---

## Quick Start

### Pre-built Image

```bash
mkdir hotbox && cd hotbox
curl -LO https://raw.githubusercontent.com/cpike5/hotbox-chat/main/docker-compose.prod.yml
curl -LO https://raw.githubusercontent.com/cpike5/hotbox-chat/main/.env.example
cp .env.example .env
# Edit .env with your secrets (see Configuration below)
docker compose -f docker-compose.prod.yml up -d
```

### Build from Source

```bash
git clone https://github.com/cpike5/hotbox-chat.git
cd hotbox-chat
cp .env.example .env
# Edit .env
docker compose up -d --build
```

---

## What Gets Created

| Container | Image | Port | Purpose |
|-----------|-------|------|---------|
| `hotbox-api` | Custom (.NET 9) | 7200 (→8080) | ASP.NET Core + Blazor Server |
| `hotbox-db` | postgres:16-alpine | Internal only | PostgreSQL database |
| `hotbox-redis` | redis:7-alpine | Internal only | Cache + SignalR backplane |
| `hotbox-seq` | datalust/seq:latest | 7201 (→80) | Structured log viewer |

All containers run on an internal Docker bridge network. Only the API (7200) and Seq UI (7201) are exposed to the host.

---

## Configuration

### Required Environment Variables

| Variable | Description | Example |
|----------|------------|---------|
| `DB_PASSWORD` | PostgreSQL password | `your-secure-db-password` |
| `JWT_SECRET` | JWT signing key (min 32 chars) | `openssl rand -base64 48` |
| `ADMIN_EMAIL` | First admin account email | `admin@example.com` |
| `ADMIN_PASSWORD` | First admin account password | `YourSecurePassword123!` |
| `ADMIN_DISPLAY_NAME` | First admin display name | `Admin` |
| `SEQ_PASSWORD` | Seq admin password (hashed) | See below |

### Generate Seq Password Hash

```bash
echo -n "yourpassword" | docker run --rm -i datalust/seq config hash
```

Copy the output hash into your `.env` file as `SEQ_PASSWORD`.

### Optional Environment Variables

| Variable | Description | Default |
|----------|------------|---------|
| `SERVER_NAME` | Server display name | `HotBox` |
| `REGISTRATION_MODE` | `Open`, `InviteOnly`, `Closed` | `Open` |
| `OAUTH_GOOGLE_CLIENT_ID` | Google OAuth client ID | — |
| `OAUTH_GOOGLE_CLIENT_SECRET` | Google OAuth client secret | — |
| `OAUTH_GOOGLE_ENABLED` | Enable Google OAuth | `false` |
| `OAUTH_MICROSOFT_CLIENT_ID` | Microsoft OAuth client ID | — |
| `OAUTH_MICROSOFT_CLIENT_SECRET` | Microsoft OAuth client secret | — |
| `OAUTH_MICROSOFT_ENABLED` | Enable Microsoft OAuth | `false` |
| `OAUTH_DISCORD_CLIENT_ID` | Discord OAuth client ID | — |
| `OAUTH_DISCORD_CLIENT_SECRET` | Discord OAuth client secret | — |
| `OAUTH_DISCORD_ENABLED` | Enable Discord OAuth | `false` |
| `OTLP_ENDPOINT` | OpenTelemetry OTLP endpoint | — |
| `ELASTICSEARCH_URL` | Elasticsearch log sink URL | — |

---

## Docker Compose (Production)

```yaml
name: hotbox

services:
  api:
    image: ghcr.io/cpike5/hotbox-chat:latest
    container_name: hotbox-api
    restart: unless-stopped
    ports:
      - "7200:8080"
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ConnectionStrings__Postgres: "Host=postgres;Port=5432;Database=hotbox;Username=hotbox;Password=${DB_PASSWORD}"
      ConnectionStrings__Redis: "redis:6379"
      Jwt__Secret: "${JWT_SECRET}"
      Server__ServerName: "${SERVER_NAME:-HotBox}"
      Server__RegistrationMode: "${REGISTRATION_MODE:-Open}"
      AdminSeed__Email: "${ADMIN_EMAIL}"
      AdminSeed__Password: "${ADMIN_PASSWORD}"
      AdminSeed__DisplayName: "${ADMIN_DISPLAY_NAME}"
      Observability__SeqUrl: "http://seq:5341"
    depends_on:
      postgres:
        condition: service_healthy
      redis:
        condition: service_healthy
    networks:
      - internal

  postgres:
    image: postgres:16-alpine
    container_name: hotbox-db
    restart: unless-stopped
    environment:
      POSTGRES_DB: hotbox
      POSTGRES_USER: hotbox
      POSTGRES_PASSWORD: ${DB_PASSWORD}
    volumes:
      - postgres-data:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U hotbox -d hotbox"]
      interval: 10s
      timeout: 5s
      retries: 5
    networks:
      - internal

  redis:
    image: redis:7-alpine
    container_name: hotbox-redis
    restart: unless-stopped
    command: redis-server --save 60 1 --loglevel warning
    volumes:
      - redis-data:/data
    healthcheck:
      test: ["CMD", "redis-cli", "ping"]
      interval: 10s
      timeout: 5s
      retries: 5
    networks:
      - internal

  seq:
    image: datalust/seq:latest
    container_name: hotbox-seq
    restart: unless-stopped
    ports:
      - "7201:80"
    environment:
      ACCEPT_EULA: Y
      SEQ_FIRSTRUN_ADMINPASSWORDHASH: ${SEQ_PASSWORD}
    volumes:
      - seq-data:/data
    networks:
      - internal

volumes:
  postgres-data:
  redis-data:
  seq-data:

networks:
  internal:
    driver: bridge
```

---

## Dockerfile

```dockerfile
# =============================================================================
# Build stage
# =============================================================================
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files first for layer caching
COPY HotBox.sln ./
COPY src/HotBox.Core/HotBox.Core.csproj src/HotBox.Core/
COPY src/HotBox.Infrastructure/HotBox.Infrastructure.csproj src/HotBox.Infrastructure/
COPY src/HotBox.Application/HotBox.Application.csproj src/HotBox.Application/
COPY src/HotBox.Client/HotBox.Client.csproj src/HotBox.Client/
RUN dotnet restore

# Copy source and publish
COPY . .
RUN dotnet publish src/HotBox.Application/HotBox.Application.csproj \
    -c Release -o /app/publish --no-restore

# =============================================================================
# Runtime stage
# =============================================================================
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime

# Non-root user
RUN groupadd -r hotbox && useradd -r -g hotbox -s /bin/false hotbox

WORKDIR /app
COPY --from=build /app/publish .

# Health check
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

USER hotbox
EXPOSE 8080
ENTRYPOINT ["dotnet", "HotBox.Application.dll"]
```

---

## TLS / Reverse Proxy

HotBox runs on HTTP internally. Use a reverse proxy for TLS termination.

### Nginx + Let's Encrypt

**Step 1: Install Certbot and Nginx**
```bash
sudo apt install -y nginx certbot python3-certbot-nginx
```

**Step 2: Initial Nginx config (HTTP only, for cert issuance)**
```nginx
server {
    listen 80;
    server_name chat.example.com;

    location / {
        proxy_pass http://127.0.0.1:7200;
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

**Step 3: Obtain certificate**
```bash
sudo certbot --nginx -d chat.example.com
```

**Step 4: Certbot rewrites the config to include SSL. Verify WebSocket support:**
```nginx
server {
    listen 443 ssl;
    server_name chat.example.com;

    ssl_certificate /etc/letsencrypt/live/chat.example.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/chat.example.com/privkey.pem;

    location / {
        proxy_pass http://127.0.0.1:7200;
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
    }
}

server {
    listen 80;
    server_name chat.example.com;
    return 301 https://$host$request_uri;
}
```

**Important**: The `proxy_read_timeout 86400s` is critical for SignalR WebSocket connections. Without it, Nginx will close idle connections after 60 seconds.

### Caddy (Alternative)

```
chat.example.com {
    reverse_proxy localhost:7200
}
```

Caddy automatically provisions TLS via Let's Encrypt and handles WebSocket proxying.

---

## Backups

### PostgreSQL

```bash
# Dump
docker exec hotbox-db pg_dump -U hotbox hotbox > backup_$(date +%Y%m%d).sql

# Restore
cat backup_20260322.sql | docker exec -i hotbox-db psql -U hotbox hotbox
```

### Redis

Redis persistence is configured with `save 60 1` (snapshot every 60s if at least 1 key changed). The data volume (`redis-data`) persists across restarts.

For a manual snapshot:
```bash
docker exec hotbox-redis redis-cli BGSAVE
```

### Volumes

Back up all Docker volumes:
```bash
docker run --rm -v hotbox_postgres-data:/data -v $(pwd):/backup alpine \
    tar czf /backup/postgres-data.tar.gz -C /data .

docker run --rm -v hotbox_redis-data:/data -v $(pwd):/backup alpine \
    tar czf /backup/redis-data.tar.gz -C /data .
```

---

## Updating

```bash
# Pull latest image
docker compose pull

# Recreate with zero downtime (if using a load balancer)
docker compose up -d --remove-orphans

# Or full restart
docker compose down && docker compose up -d
```

EF Core migrations run automatically on startup. The API container will apply any pending migrations before accepting requests.

---

## Optional: TURN Server

For voice chat in restrictive NAT environments, add a coturn TURN server:

```yaml
  coturn:
    image: coturn/coturn:latest
    container_name: hotbox-turn
    restart: unless-stopped
    network_mode: host
    volumes:
      - ./turnserver.conf:/etc/coturn/turnserver.conf
```

`turnserver.conf`:
```
listening-port=3478
realm=chat.example.com
user=hotbox:turn-secret
lt-cred-mech
fingerprint
no-cli
```

Then add to `appsettings.json`:
```json
{
  "IceServers": [
    { "Urls": ["stun:stun.l.google.com:19302"] },
    { "Urls": ["turn:chat.example.com:3478"], "Username": "hotbox", "Credential": "turn-secret" }
  ]
}
```

---

## Optional: Elastic Stack Integration

If you run a separate ELK stack, connect HotBox to it:

```yaml
  api:
    environment:
      Observability__OtlpEndpoint: "http://apm-server:8200"
      Observability__ElasticsearchUrl: "http://elasticsearch:9200"
    networks:
      - internal
      - elastic

networks:
  elastic:
    external: true    # Defined in your ELK compose stack
```

See the `my-stack.md` Docker Compose template for a full ELK + Kafka stack reference.

---

## Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| API won't start | Database not ready | Check `depends_on` health conditions; verify PostgreSQL healthcheck |
| SignalR disconnects after 60s | Nginx timeout | Add `proxy_read_timeout 86400s` to Nginx config |
| Redis connection refused | Redis not healthy | Check Redis healthcheck; verify `ConnectionStrings:Redis` |
| Voice chat not connecting | NAT traversal failure | Add a TURN server (see above) |
| Seq not receiving logs | Wrong URL | Verify `Observability:SeqUrl` matches Seq container name and port |
| Migrations fail | PostgreSQL version | Ensure PostgreSQL 16+ (required for some features) |
