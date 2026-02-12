FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files first for layer caching
COPY HotBox.sln ./
COPY src/HotBox.Core/HotBox.Core.csproj src/HotBox.Core/
COPY src/HotBox.Infrastructure/HotBox.Infrastructure.csproj src/HotBox.Infrastructure/
COPY src/HotBox.Application/HotBox.Application.csproj src/HotBox.Application/
COPY src/HotBox.Client/HotBox.Client.csproj src/HotBox.Client/

# Restore dependencies (app only, skip test projects)
RUN dotnet restore src/HotBox.Application/HotBox.Application.csproj

# Copy everything else and build
COPY src/ src/
RUN dotnet publish src/HotBox.Application/HotBox.Application.csproj \
    -c Release \
    -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

RUN adduser --disabled-password --gecos '' hotbox \
    && mkdir -p /data \
    && chown hotbox:hotbox /data
USER hotbox

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "HotBox.Application.dll"]
