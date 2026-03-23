# Full .NET Stack Reference

## Frontend — Blazor
Blazor Web · MudBlazor · Radzen · Blazored (LocalStorage, Modal, Toast) · Floating UI · Sortable.js

> **MudBlazor vs Radzen**
>
> | | MudBlazor | Radzen |
> |---|---|---|
> | **Best for** | Clean Material UI, community support | Rapid scaffolding, component breadth |
> | **Components** | ~70 | ~90+ |
> | **Cost** | Free (MIT) | Free core, paid Studio |
> | **Design** | Material Design | Flexible/neutral |
> | **Tooling** | Manual | Visual designer available |
> | **Community** | Larger | Smaller |
>
> Use MudBlazor for primary UI structure; Radzen for specialized components (Scheduler, HtmlEditor, GoogleMap) you'd otherwise build from scratch.

## Frontend — Desktop
Avalonia UI

## CSS / Icons
Tailwind · Hero Icons · Lucide · Font Awesome

> **Hero Icons vs Lucide vs Font Awesome**
>
> | | Hero Icons | Lucide | Font Awesome |
> |---|---|---|---|
> | **Best for** | Tailwind projects | General-purpose SVG | Maximum coverage |
> | **Icon count** | ~300 | ~1500+ | 2000+ free / 26000+ pro |
> | **Cost** | Free | Free | Free tier + paid Pro |
> | **Styles** | Outline, Solid, Mini | Stroke | Solid, Regular, Light, Thin, Duotone |
> | **Brand icons** | No | No | Yes |
> | **Format** | SVG | SVG | Font + SVG |
>
> Use Hero/Lucide for UI icons; Font Awesome when you need brand logos or niche icons.

## Charts
ApexCharts · Chart.js

> **ApexCharts vs Chart.js**
> - **ApexCharts** — rich out-of-the-box interactivity (zoom, pan, tooltips, annotations), more chart types (heatmap, treemap, radar), has a Blazor wrapper. Use for dashboards and data-heavy pages.
> - **Chart.js** — lighter weight, simpler API, massive ecosystem. Use for straightforward charts in prototypes, landing pages, or when bundle size matters.

## APIs
ASP.NET Core (controllers) · Scalar · Refit · Rate Limiting (built-in) · Polly v8

## Messaging & Jobs
MassTransit v8 · Apache Kafka · Quartz.NET · BackgroundService

> **When to use what**
> - **BackgroundService** — simple in-process work: polling, queue draining, warmup tasks. No persistence or retry.
> - **Quartz.NET** — scheduled/recurring jobs (cron-style). Persistent job store, misfire handling, good for reports and cleanup tasks.
> - **MassTransit** — service-to-service messaging, command/event patterns, sagas. Abstracts the transport so you can swap brokers.
> - **Kafka** — high-throughput event streaming, ordered log processing, event sourcing. Use when you need durable replay or fan-out at scale.

## Validation
FluentValidation

## Auth & Security
ASP.NET Core Identity · OAuth (Google, Microsoft, Discord) · HaveIBeenPwned validator

## Data
EF Core · DbUp · MSSQL · PostgreSQL

## Caching
IMemoryCache · Redis · HybridCache (.NET 9+)

> **When to use what**
> - **IMemoryCache** — single-server, short-lived, hot-path data (current user, lookup tables). Lost on restart.
> - **Redis** — shared across instances, survives restarts, needed for multi-server or session/rate-limit state.
> - **HybridCache** — best of both: L1 in-memory + L2 Redis backplane. Use as the default on .NET 9+ to avoid choosing.

## Document Generation
QuestPDF · Markdig

## Communication
FluentEmail + SMTP

## Feature Management
Microsoft.FeatureManagement

## Health Checks
AspNetCore.HealthChecks.*

## Audit
Audit.NET (EF Core + Elasticsearch sink)

## Testing
xUnit · FluentAssertions · NSubstitute · Testcontainers · Respawn

## Observability
Serilog · Elastic APM · OpenTelemetry · Elasticsearch · Kibana · Seq

## CLI Tooling
Spectre.Console · System.CommandLine

---

## Docker Compose Template

> Elastic (ELK + APM) runs on a separate compose stack exposed via the `elastic` external network.
> Seq runs in this stack for local structured log viewing.
>
> In your ELK compose, declare the shared network:
> ```yaml
> networks:
>   elastic:
>     name: elastic
>     driver: bridge
> ```
>
> Secrets are loaded from a `.env` file alongside this compose file — keep it out of source control.
> ```
> POSTGRES_PASSWORD=yourpasswordhere
> SEQ_PASSWORD=yourpasswordhere
> ```
>
> `SEQ_PASSWORD` must be a hashed password, not plaintext. Generate it with:
> ```bash
> echo -n "yourpassword" | docker run --rm -i datalust/seq config hash
> ```
> Copy the output hash into your `.env` file.

```yaml
# =============================================================================
# App Stack — docker-compose.yml
# =============================================================================

name: app-stack

services:

  # ---------------------------------------------------------------------------
  # .NET Application
  # ---------------------------------------------------------------------------
  api:
    build:
      context: .
      dockerfile: Dockerfile
    container_name: app-api
    restart: unless-stopped
    ports:
      - "5000:8080"
    environment:
      ASPNETCORE_ENVIRONMENT: Development
      ConnectionStrings__Postgres: "Host=postgres;Port=5432;Database=appdb;Username=app;Password=${POSTGRES_PASSWORD}"
      ConnectionStrings__Redis: "redis:6379"
      Kafka__BootstrapServers: "kafka:9092"
      Seq__ServerUrl: "http://seq:5341"
      # Elastic APM — points to your external ELK stack
      ElasticApm__ServerUrl: "http://apm-server:8200"
      ElasticApm__ServiceName: "app-api"
      ElasticApm__Environment: "development"
    depends_on:
      postgres:
        condition: service_healthy
      redis:
        condition: service_healthy
      kafka:
        condition: service_healthy
      seq:
        condition: service_started
    networks:
      - internal
      - elastic

  # ---------------------------------------------------------------------------
  # PostgreSQL
  # ---------------------------------------------------------------------------
  postgres:
    image: postgres:16-alpine
    container_name: app-postgres
    restart: unless-stopped
    ports:
      - "5432:5432"           # expose for local tooling (pgAdmin, Rider, etc.)
    environment:
      POSTGRES_DB: appdb
      POSTGRES_USER: app
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
    volumes:
      - postgres-data:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U app -d appdb"]
      interval: 10s
      timeout: 5s
      retries: 5
    networks:
      - internal

  # ---------------------------------------------------------------------------
  # Redis
  # ---------------------------------------------------------------------------
  redis:
    image: redis:7-alpine
    container_name: app-redis
    restart: unless-stopped
    ports:
      - "6379:6379"           # expose for local inspection (RedisInsight, etc.)
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

  # ---------------------------------------------------------------------------
  # Kafka (KRaft mode — no Zookeeper)
  # ---------------------------------------------------------------------------
  kafka:
    image: bitnami/kafka:3.7
    container_name: app-kafka
    restart: unless-stopped
    ports:
      - "9092:9092"           # expose for local producers/consumers
    environment:
      KAFKA_CFG_NODE_ID: 0
      KAFKA_CFG_PROCESS_ROLES: controller,broker
      KAFKA_CFG_CONTROLLER_QUORUM_VOTERS: "0@kafka:9093"
      KAFKA_CFG_LISTENERS: PLAINTEXT://:9092,CONTROLLER://:9093
      KAFKA_CFG_ADVERTISED_LISTENERS: PLAINTEXT://kafka:9092
      KAFKA_CFG_LISTENER_SECURITY_PROTOCOL_MAP: PLAINTEXT:PLAINTEXT,CONTROLLER:PLAINTEXT
      KAFKA_CFG_CONTROLLER_LISTENER_NAMES: CONTROLLER
      KAFKA_CFG_INTER_BROKER_LISTENER_NAME: PLAINTEXT
      KAFKA_CFG_AUTO_CREATE_TOPICS_ENABLE: "true"
      KAFKA_CFG_DEFAULT_REPLICATION_FACTOR: 1
      KAFKA_CFG_OFFSETS_TOPIC_REPLICATION_FACTOR: 1
      KAFKA_CFG_TRANSACTION_STATE_LOG_REPLICATION_FACTOR: 1
      KAFKA_CFG_TRANSACTION_STATE_LOG_MIN_ISR: 1
    volumes:
      - kafka-data:/bitnami/kafka
    healthcheck:
      test: ["CMD-SHELL", "kafka-topics.sh --bootstrap-server localhost:9092 --list"]
      interval: 15s
      timeout: 10s
      retries: 5
      start_period: 30s
    networks:
      - internal

  # ---------------------------------------------------------------------------
  # Kafka UI
  # ---------------------------------------------------------------------------
  kafka-ui:
    image: provectuslabs/kafka-ui:latest
    container_name: app-kafka-ui
    restart: unless-stopped
    ports:
      - "8080:8080"
    environment:
      KAFKA_CLUSTERS_0_NAME: local
      KAFKA_CLUSTERS_0_BOOTSTRAPSERVERS: kafka:9092
    depends_on:
      kafka:
        condition: service_healthy
    networks:
      - internal

  # ---------------------------------------------------------------------------
  # Seq — structured log viewer
  # ---------------------------------------------------------------------------
  seq:
    image: datalust/seq:latest
    container_name: app-seq
    restart: unless-stopped
    ports:
      - "5341:5341"           # ingest
      - "8081:80"             # UI — http://localhost:8081
    environment:
      ACCEPT_EULA: Y
      SEQ_FIRSTRUN_ADMINPASSWORDHASH: ${SEQ_PASSWORD}
    volumes:
      - seq-data:/data
    networks:
      - internal

# =============================================================================
# Volumes
# =============================================================================
# Two options — named volumes (Docker manages the path) or host path mounts
# (you control exactly where data lives on the host).
#
# Option A — named volumes (default, recommended for most cases).
# Docker manages storage under /var/lib/docker/volumes/ on Linux.
# Clean, portable, easy to back up with `docker volume` commands.
#
volumes:
  postgres-data:
  redis-data:
  kafka-data:
  seq-data:
#
# Option B — host path mounts (swap the volumes: block above and update
# each service's volumes: entry to match). Example per service:
#
#   postgres:
#     volumes:
#       - ./data/postgres:/var/lib/postgresql/data
#
#   redis:
#     volumes:
#       - ./data/redis:/data
#
#   kafka:
#     volumes:
#       - ./data/kafka:/bitnami/kafka
#
#   seq:
#     volumes:
#       - ./data/seq:/data
#
# Host paths are relative to docker-compose.yml. Using ./data/* keeps
# everything under one folder you can back up or inspect easily.
# Add ./data to your .gitignore.
#
# On Linux, Bitnami images run as non-root (uid 1001) — set permissions first:
#   mkdir -p ./data/kafka && sudo chown -R 1001:1001 ./data/kafka

# =============================================================================
# Networks
# =============================================================================
networks:
  internal:
    driver: bridge
  elastic:
    external: true            # defined in your ELK compose stack
```
