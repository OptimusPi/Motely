# Motely Distributed Worker Deployment Guide

**Deploy Motely.API + Motely.DistributedWorker on cheap Linux servers with AVX-512 SIMD**

---

## 🎯 Requirements Recap

| Requirement | Notes |
|-------------|-------|
| **CPU** | AVX-512F required for SIMD acceleration |
| **RAM** | 512MB-1GB sufficient (Motely is CPU-bound, not memory-bound) |
| **Storage** | <5GB needed (just OS + .NET runtime + binaries) |
| **Network** | Cloudflare Tunnel OR public IPv4 |
| **Latency** | Doesn't matter — batch processing, not real-time |

---

## 💰 Hosting Comparison: Cheap AVX-512 Servers

### Which CPUs have AVX-512?

| CPU Family | AVX-512 Support | Notes |
|------------|-----------------|-------|
| **AMD EPYC Genoa (4th gen)** | ✅ Full 512-bit | Best price/perf for AVX-512 |
| **AMD EPYC Milan (3rd gen)** | ❌ No | Only AVX2 |
| **AMD Ryzen 7000 (Zen 4)** | ✅ 256-bit double-pumped | Works but half speed |
| **AMD Ryzen 5000 (Zen 3)** | ❌ No | Only AVX2 |
| **Intel Xeon Scalable (Ice Lake+)** | ✅ Full 512-bit | Good but pricier |
| **Intel Xeon E-2300** | ✅ Full 512-bit | Budget Xeon option |
| **Intel Core 11th-12th gen** | ✅ P-cores only | Consumer chips, rare in servers |

### 🏆 RECOMMENDED: Cheapest AVX-512 Dedicated Servers

| Provider | Server | CPU | Cores | RAM | Storage | Price/mo | AVX-512 | Link |
|----------|--------|-----|-------|-----|---------|----------|---------|------|
| **Hetzner** | AX42 | AMD Ryzen 7 7700 | 8c/16t | 64GB | 2×512GB NVMe | **€52** (~$57) | ✅ Zen 4 | [hetzner.com](https://www.hetzner.com/dedicated-rootserver/ax42/) |
| **Hetzner** | AX102 | AMD Ryzen 9 7950X3D | 16c/32t | 128GB | 2×1TB NVMe | **€104** (~$114) | ✅ Zen 4 | [hetzner.com](https://www.hetzner.com/dedicated-rootserver/ax102/) |
| **Hetzner** | AX162-R | AMD EPYC 9454P | 48c/96t | 256GB | 2×1.92TB NVMe | **€199** (~$218) | ✅ Full | [hetzner.com](https://www.hetzner.com/dedicated-rootserver/ax162/) |
| **Contabo** | Ryzen 12 | AMD Ryzen 9 7900 | 12c/24t | 64GB | 1TB NVMe | **€96** (~$105) | ✅ Zen 4 | [contabo.com](https://contabo.com/en-us/dedicated-servers/amd-ryzen-12-cores/) |
| **Contabo** | Genoa 24 | AMD EPYC 9224 | 24c/48t | 128GB | 2×1TB SSD | **€179** (~$196) | ✅ Full | [contabo.com](https://contabo.com/en-us/dedicated-servers/amd-genoa-24-cores/) |
| **OVH Eco** | Rise-1 | Intel Xeon E-2386G | 6c/12t | 32GB | 2×512GB SSD | **€55** (~$60) | ✅ Full | [eco.ovhcloud.com](https://eco.ovhcloud.com/en/) |

### 🥇 BEST VALUE PICK: **Hetzner AX42** @ €52/mo

- AMD Ryzen 7 7700 (Zen 4) = AVX-512 support
- 8 cores / 16 threads @ 5.3GHz boost
- 64GB DDR5 RAM (way more than needed)
- 2×512GB NVMe (way more than needed)
- Germany datacenter (latency doesn't matter for batch work)
- **~$57/month** — hard to beat for AVX-512

### 🥈 BUDGET PICK: **OVH Eco Rise-1** @ €55/mo

- Intel Xeon E-2386G = full AVX-512
- 6 cores / 12 threads
- 32GB RAM
- France/Canada datacenters
- Often has stock issues — check availability

### 🥉 OVERKILL PICK: **Hetzner AX162-R** @ €199/mo

- AMD EPYC 9454P = 48 cores, full 512-bit AVX-512
- For running MANY workers or being a coordinator for others
- Could host 10+ worker processes

---

## 🐧 Recommended Linux Distro

| Distro | Recommendation | Why |
|--------|----------------|-----|
| **Debian 12 (Bookworm)** | ✅ **BEST** | Stable, minimal, .NET 10 packages available |
| **Ubuntu 24.04 LTS** | ✅ Good | More packages, slightly heavier |
| **Alpine Linux** | ⚠️ Advanced | Smallest footprint, musl libc quirks |
| **Fedora 40+** | ✅ Good | Bleeding edge, .NET well supported |

**Pick Debian 12** — it's the default on Hetzner/Contabo/OVH and works perfectly.

---

## 🚀 Installation Guide (Debian 12 / Ubuntu 24.04)

### Step 1: Initial Server Setup

```bash
# SSH into your new server
ssh root@YOUR_SERVER_IP

# Update system
apt update && apt upgrade -y

# Install essentials
apt install -y curl wget git unzip htop

# Create a non-root user (optional but recommended)
adduser motely
usermod -aG sudo motely
su - motely
```

### Step 2: Install .NET 10 Runtime

```bash
# Add Microsoft package repository
wget https://packages.microsoft.com/config/debian/12/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb

# Install .NET 10 runtime (not SDK — we're running pre-built binaries)
sudo apt update
sudo apt install -y dotnet-runtime-10.0

# Verify
dotnet --info
```

### Step 3: Verify AVX-512 Support

```bash
# Check CPU flags
grep -o 'avx512[a-z]*' /proc/cpuinfo | sort -u

# You should see:
# avx512f
# avx512bw
# avx512cd
# avx512dq
# avx512vl
# (and possibly more)

# If you see avx512f, you're good!
```

### Step 4: Download Motely Binaries

```bash
# Create app directory
mkdir -p ~/motely
cd ~/motely

# Option A: Download from GitHub Releases (when published)
# wget https://github.com/OptimusPi/MotelyJAML/releases/latest/download/Motely.API-linux-x64.tar.gz
# wget https://github.com/OptimusPi/MotelyJAML/releases/latest/download/Motely.DistributedWorker-linux-x64.tar.gz
# tar -xzf Motely.API-linux-x64.tar.gz
# tar -xzf Motely.DistributedWorker-linux-x64.tar.gz

# Option B: Build locally and SCP (for now)
# On your Windows machine:
# dotnet publish Motely.API -c Release -r linux-x64 --self-contained
# dotnet publish Motely.DistributedWorker -c Release -r linux-x64 --self-contained
# scp -r bin/Release/net10.0/linux-x64/publish/* motely@YOUR_SERVER:~/motely/

# Make executable
chmod +x ~/motely/Motely.API
chmod +x ~/motely/Motely.DistributedWorker
```

### Step 5: Configure Motely.API

```bash
# Create config file
cat > ~/motely/appsettings.json << 'EOF'
{
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  },
  "AllowedHosts": "*",
  "Urls": "http://0.0.0.0:5000"
}
EOF
```

### Step 6: Run Motely.API as a Service

```bash
# Create systemd service
sudo tee /etc/systemd/system/motely-api.service << 'EOF'
[Unit]
Description=Motely API Server
After=network.target

[Service]
Type=simple
User=motely
WorkingDirectory=/home/motely/motely
ExecStart=/home/motely/motely/Motely.API
Restart=always
RestartSec=10
Environment=DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1

[Install]
WantedBy=multi-user.target
EOF

# Enable and start
sudo systemctl daemon-reload
sudo systemctl enable motely-api
sudo systemctl start motely-api

# Check status
sudo systemctl status motely-api

# View logs
sudo journalctl -u motely-api -f
```

### Step 7: Run Distributed Worker(s)

```bash
# Create worker service (can run multiple instances)
sudo tee /etc/systemd/system/motely-worker@.service << 'EOF'
[Unit]
Description=Motely Distributed Worker %i
After=network.target motely-api.service

[Service]
Type=simple
User=motely
WorkingDirectory=/home/motely/motely
ExecStart=/home/motely/motely/Motely.DistributedWorker --url http://localhost:5000 --threads 4
Restart=always
RestartSec=10
Environment=DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1

[Install]
WantedBy=multi-user.target
EOF

# Enable workers (e.g., 2 workers using 4 threads each on an 8-core CPU)
sudo systemctl daemon-reload
sudo systemctl enable motely-worker@1
sudo systemctl enable motely-worker@2
sudo systemctl start motely-worker@1
sudo systemctl start motely-worker@2
```

---

## 🌐 Expose via Cloudflare Tunnel (Recommended)

No need to open ports or get a static IP!

### Step 1: Install cloudflared

```bash
# Download and install
curl -L https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-linux-amd64.deb -o cloudflared.deb
sudo dpkg -i cloudflared.deb
rm cloudflared.deb

# Verify
cloudflared --version
```

### Step 2: Authenticate with Cloudflare

```bash
cloudflared tunnel login
# Opens browser to authenticate with your Cloudflare account
```

### Step 3: Create a Tunnel

```bash
# Create tunnel
cloudflared tunnel create motely-api

# Note the tunnel ID (e.g., a1b2c3d4-e5f6-7890-abcd-ef1234567890)
```

### Step 4: Configure the Tunnel

```bash
# Create config
mkdir -p ~/.cloudflared
cat > ~/.cloudflared/config.yml << 'EOF'
tunnel: YOUR_TUNNEL_ID
credentials-file: /home/motely/.cloudflared/YOUR_TUNNEL_ID.json

ingress:
  - hostname: motely-api.yourdomain.com
    service: http://localhost:5000
  - service: http_status:404
EOF

# Replace YOUR_TUNNEL_ID with actual ID
```

### Step 5: Add DNS Record

```bash
cloudflared tunnel route dns motely-api motely-api.yourdomain.com
```

### Step 6: Run Tunnel as Service

```bash
sudo cloudflared service install
sudo systemctl start cloudflared
sudo systemctl enable cloudflared

# Verify
curl https://motely-api.yourdomain.com/api/health
```

---

## 🔌 Alternative: Direct IPv4 (Simpler but Less Secure)

If you have a public IPv4 and don't want Cloudflare:

```bash
# Open firewall port
sudo ufw allow 5000/tcp

# Or with iptables
sudo iptables -A INPUT -p tcp --dport 5000 -j ACCEPT

# Access directly
curl http://YOUR_SERVER_IP:5000/api/health
```

---

## 🏗️ Architecture Options

### Option A: v0 as Central Coordinator (Recommended)

```
┌─────────────────────────────────────────────────────────────┐
│                    v0-balatro-seed-hosting                  │
│                  (Vercel + Neon Postgres)                   │
│                                                             │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────────────┐ │
│  │ /api/search │  │ /api/submit │  │ Neon DB (sessions,  │ │
│  │   /create   │  │   /results  │  │ results, workers)   │ │
│  └──────┬──────┘  └──────┬──────┘  └─────────────────────┘ │
└─────────┼────────────────┼──────────────────────────────────┘
          │                │
          ▼                ▼
    ┌─────────────────────────────────────┐
    │         Cloudflare Tunnel           │
    └─────────────────────────────────────┘
          │                │
    ┌─────┴─────┐    ┌─────┴─────┐
    │  Worker 1 │    │  Worker 2 │    ...
    │ (Hetzner) │    │ (Contabo) │
    │ AVX-512   │    │ AVX-512   │
    └───────────┘    └───────────┘
```

**Pros:**
- Single source of truth (Neon DB)
- v0 already has auth, rate limiting, etc.
- Workers just need to know v0 URL

**Cons:**
- Vercel serverless has cold starts
- Need to add coordinator endpoints to v0

### Option B: Motely.API as Coordinator

```
┌─────────────────────────────────────────────────────────────┐
│              Motely.API (on dedicated server)               │
│                                                             │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────────────┐ │
│  │ /api/search │  │ /api/claim  │  │ In-memory sessions  │ │
│  │   /start    │  │ /api/submit │  │ (or SQLite/DuckDB)  │ │
│  └──────┬──────┘  └──────┬──────┘  └─────────────────────┘ │
└─────────┼────────────────┼──────────────────────────────────┘
          │                │
    ┌─────┴─────┐    ┌─────┴─────┐
    │  Worker 1 │    │  Worker 2 │    ...
    │ (same box)│    │ (remote)  │
    └───────────┘    └───────────┘
```

**Pros:**
- Self-contained, no external dependencies
- Faster (no network hops for local workers)
- Already implemented in Motely.API

**Cons:**
- Single point of failure
- Need to expose API publicly

### Option C: Self-Identifying Mesh (FUTURE)

```
┌───────────────────────────────────────────────────────────────┐
│                    Cloudflare Workers KV                      │
│              (Global registry of Motely nodes)                │
└───────────────────────────────────────────────────────────────┘
          │                │                │
    ┌─────┴─────┐    ┌─────┴─────┐    ┌─────┴─────┐
    │  Node 1   │◄──►│  Node 2   │◄──►│  Node 3   │
    │ (Hetzner) │    │ (Contabo) │    │ (Proxmox) │
    │ API+Worker│    │ API+Worker│    │ API+Worker│
    └───────────┘    └───────────┘    └───────────┘
```

**How it would work:**
1. Each Motely.API registers itself with Cloudflare KV on startup
2. Nodes discover each other via KV
3. Search requests are load-balanced across nodes
4. Results aggregated via gossip protocol or central KV

**Pros:**
- Fully decentralized
- No single point of failure
- Community can contribute nodes

**Cons:**
- Complex to implement
- Needs consensus for deduplication
- Future work

---

## 📋 Quick Start Checklist

### For Proxmox (Your Existing Cluster)

```bash
# 1. Create Debian 12 VM (2 cores, 1GB RAM, 10GB disk)
# 2. SSH in and run:

# Install .NET 10
wget https://packages.microsoft.com/config/debian/12/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb && rm packages-microsoft-prod.deb
sudo apt update && sudo apt install -y dotnet-runtime-10.0

# Verify AVX-512 (Proxmox must pass through host CPU flags)
grep avx512f /proc/cpuinfo

# Download binaries (from your Windows build)
mkdir ~/motely && cd ~/motely
# scp from Windows...

# Run worker connecting to your main coordinator
./Motely.DistributedWorker --url https://motely-api.yourdomain.com --threads 4
```

### For New Hetzner/Contabo Server

1. Order server (Hetzner AX42 recommended)
2. Install Debian 12 (default option)
3. Follow full installation guide above
4. Set up Cloudflare Tunnel
5. Run Motely.API + workers

---

## 🔧 Tuning for Maximum Performance

```bash
# Check SIMD support at runtime
./Motely.DistributedWorker --info

# Optimal thread count = physical cores (not hyperthreads)
# For Ryzen 7 7700 (8 cores): --threads 8
# For EPYC 9454P (48 cores): --threads 48

# Monitor CPU usage
htop
# All cores should be at 100% during search

# Check seeds/second in logs
sudo journalctl -u motely-worker@1 -f
# Look for: "Searched 1,000,000 seeds in 250ms (4M seeds/sec)"
```

---

## 🎉 Summary

| What | Where | Cost |
|------|-------|------|
| **Coordinator** | v0 (Vercel) or Motely.API (dedicated) | Free / €52/mo |
| **Workers** | Hetzner AX42 / Contabo Ryzen 12 | €52-96/mo each |
| **Networking** | Cloudflare Tunnel | Free |
| **Database** | Neon (v0) or in-memory | Free |

**Minimum viable setup:** 1× Hetzner AX42 @ €52/mo running both API + workers.

**Scale up:** Add more workers on cheap servers, all connecting to one coordinator.

**Community mode:** Let others run workers that connect to your coordinator (needs auth).

---

## 🔗 Links

- [Hetzner Dedicated Servers](https://www.hetzner.com/dedicated-rootserver/)
- [Contabo Dedicated Servers](https://contabo.com/en-us/dedicated-servers/)
- [OVH Eco Servers](https://eco.ovhcloud.com/en/)
- [Cloudflare Tunnel Docs](https://developers.cloudflare.com/cloudflare-one/connections/connect-networks/)
- [.NET 10 Linux Install](https://learn.microsoft.com/en-us/dotnet/core/install/linux)
