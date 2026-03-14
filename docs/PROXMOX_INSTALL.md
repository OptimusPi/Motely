# MotelyJAML — Linux / Proxmox Server Install

## What to install

| Component | What it does | Need CloudFlare Tunnel? |
|-----------|-------------|------------------------|
| `Motely.API` | Local REST API + Dashboard + WASM search UI | Only if you want public access. Otherwise: NO. |
| `Motely.DistributedWorker` | Background helper — pulls blocks from queue, submits results | **NO** — it connects OUT to JAMMY, never receives inbound traffic. |

**TL;DR**: A distributed worker is pull-only. It calls out to `seedfinder.app` to fetch work and post results. No inbound port or tunnel needed.

---

## Prerequisites

```bash
# Install .NET 10 (AOT-published binaries are self-contained, but runtime needed for non-AOT)
curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 10.0
export PATH="$HOME/.dotnet:$PATH"
```

For the self-contained AOT publish (no .NET needed on server):

```bash
# The published binary is a single native executable — nothing to install
chmod +x ./Motely.API
./Motely.API
```

---

## Install Motely.API

```bash
# On your dev machine: publish self-contained for linux-x64
dotnet publish x:/JammySeedFinder/src/MotelyJAML/Motely.API/Motely.API.csproj \
  -c Release -r linux-x64 --self-contained true \
  -o ./publish/motely-api

# Copy to server
scp -r ./publish/motely-api user@proxmox-host:/opt/motely-api

# On the server
chmod +x /opt/motely-api/Motely.API
```

**Configure** (`/opt/motely-api/appsettings.json`):

```json
{
  "Jaml": {
    "Directory": "/opt/motely-filters"
  },
  "PoolWorker": {
    "Url": "https://seedfinder.app",
    "WorkerId": "my-server",
    "Threads": 16
  },
  "Urls": "http://0.0.0.0:5000"
}
```

**Systemd service** (`/etc/systemd/system/motely-api.service`):

```ini
[Unit]
Description=Motely API
After=network.target

[Service]
WorkingDirectory=/opt/motely-api
ExecStart=/opt/motely-api/Motely.API
Restart=always
RestartSec=5
Environment=ASPNETCORE_ENVIRONMENT=Production

[Install]
WantedBy=multi-user.target
```

```bash
systemctl enable --now motely-api
systemctl status motely-api
# Dashboard at http://server-ip:5000
```

---

## Install Motely.DistributedWorker (headless helper)

```bash
dotnet publish x:/JammySeedFinder/src/MotelyJAML/Motely.DistributedWorker/Motely.DistributedWorker.csproj \
  -c Release -r linux-x64 --self-contained true \
  -o ./publish/motely-worker

scp -r ./publish/motely-worker user@proxmox-host:/opt/motely-worker
chmod +x /opt/motely-worker/Motely.DistributedWorker
```

**Configure** (`/opt/motely-worker/appsettings.json`):

```json
{
  "PoolWorker": {
    "Url": "https://seedfinder.app",
    "WorkerId": "proxmox-worker-1",
    "Threads": 32
  }
}
```

**Systemd service** (`/etc/systemd/system/motely-worker.service`):

```ini
[Unit]
Description=Motely Distributed Worker
After=network.target

[Service]
WorkingDirectory=/opt/motely-worker
ExecStart=/opt/motely-worker/Motely.DistributedWorker
Restart=always
RestartSec=10
Environment=ASPNETCORE_ENVIRONMENT=Production

[Install]
WantedBy=multi-user.target
```

```bash
systemctl enable --now motely-worker
journalctl -u motely-worker -f   # watch logs
```

---

## Allowing anonymous seed submissions

Anyone can submit seeds to a filter session without authentication. The endpoint:

```
POST https://seedfinder.app/api/queue/submit-results
{ "filterId": "<uuid>", "seeds": [...] }
```

No API key required (JAMMY validates the filterId exists before storing). This means any community member running Motely.DistributedWorker can contribute seeds to active sessions.

---

## Multiple Proxmox nodes

Run `Motely.DistributedWorker` on as many nodes as you like — they all pull from the same Vercel Queue. Vercel handles deduplication via idempotency keys (each block message has a unique key). Two workers claiming the same block will just produce duplicate results that get merged in Neon.

---

## Do I need CloudFlare Tunnel?

- **Motely.API** (local dashboard only): No tunnel needed.
- **Motely.API** (want to access from internet): Yes, set up a tunnel pointing to port 5000.
- **Motely.DistributedWorker**: Never needs a tunnel — outbound only.
