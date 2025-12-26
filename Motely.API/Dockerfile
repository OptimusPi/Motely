# Balatro Seed Oracle MCP Server - Docker Image
# Multi-stage build for smaller image size

# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files
COPY Motely.API/Motely.API.csproj Motely.API/
COPY Motely/Motely.csproj Motely/
COPY Directory.Packages.props .

# Restore dependencies
RUN dotnet restore Motely.API/Motely.API.csproj

# Copy source code
COPY Motely.API/ Motely.API/
COPY Motely/ Motely/

# Build and publish
WORKDIR /src/Motely.API
RUN dotnet publish -c Release -o /app/publish \
    -p:PublishSingleFile=true \
    -p:SelfContained=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:EnableCompressionInSingleFile=true \
    -r linux-x64

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app

# Install required dependencies for DuckDB and health check
RUN apt-get update && \
    apt-get install -y --no-install-recommends \
    libc6 \
    libgcc1 \
    libstdc++6 \
    curl \
    && rm -rf /var/lib/apt/lists/*

# Copy published files
COPY --from=build /app/publish .

# Create directories for filters and data
RUN mkdir -p /app/JamlFilters /app/WordLists /app/SearchDatabases

# Set environment variables
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true

# Expose port (for HTTP transport, though stdio is primary)
EXPOSE 3141

# Default: Run in stdio mode (for MCP clients)
# The published executable will be named Motely.API (or Motely.API.exe on Windows)
# For Linux, we need to find the actual executable name
ENTRYPOINT ["./Motely.API", "--host-api"]

# Health check (for HTTP mode)
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD curl -f http://localhost:3141/mcp || exit 1

