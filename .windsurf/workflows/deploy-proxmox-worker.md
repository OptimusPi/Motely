---
description: Build and deploy MotelyWorker AOT to PROXMOX server for distributed seed search
---
# Deploy MotelyWorker to PROXMOX

## Prerequisites

1. PROXMOX server with SSH access
2. Linux VM or LXC container (Ubuntu 22.04+ recommended)
3. Neon database URL (from JAMMY deployment)

## Step 1: Build Linux AOT Binary

```powershell
# From MotelyJAML root
cd x:\JammySeedFinder\src\MotelyJAML

# Build Linux x64 AOT (single native binary, no .NET runtime needed)
dotnet publish Motely.DistributedWorker -c Release -r linux-x64 -o ./publish-linux

# Output: ./publish-linux/MotelyWorker (~15MB native binary)
```

## Step 2: Deploy to PROXMOX

```powershell
# Create directory on server
ssh root@proxmox "mkdir -p /opt/motely-worker"

# Copy binary
scp ./publish-linux/MotelyWorker root@proxmox:/opt/motely-worker/

# Copy systemd service
scp ./deploy/motely-worker.service root@proxmox:/etc/systemd/system/
```

## Step 3: Configure and Start

```bash
# SSH into server
ssh root@proxmox

# Make executable
chmod +x /opt/motely-worker/MotelyWorker

# Edit service to set your pool URL
nano /etc/systemd/system/motely-worker.service
# Change: ExecStart=/opt/motely-worker/MotelyWorker --pool https://www.seedfinder.app --threads 32

# Enable and start
systemctl daemon-reload
systemctl enable motely-worker
systemctl start motely-worker

# Check status
systemctl status motely-worker

# Watch logs
journalctl -u motely-worker -f
```

## Step 4: Scale Across Multiple VMs

For each additional VM/container:

```bash
# Clone the worker directory
ssh root@proxmox "cp -r /opt/motely-worker /opt/motely-worker-vm2"

# Create separate service with unique worker-id
scp ./deploy/motely-worker-vm2.service root@proxmox:/etc/systemd/system/

# Start
systemctl start motely-worker-vm2
```

## Monitoring

```bash
# View all worker logs
journalctl -u "motely-worker*" -f

# Check worker stats
curl http://localhost:5000/api/worker/status

# Check server status (includes worker info)
curl https://www.seedfinder.app/api/status
```

## Troubleshooting

```bash
# Worker won't start - check binary
ldd /opt/motely-worker/MotelyWorker

# Connection refused - check pool URL
curl https://www.seedfinder.app/api/search/helper

# No work available - check if search session is active
curl https://www.seedfinder.app/api/search/sessions
```
