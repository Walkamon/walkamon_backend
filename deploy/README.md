# Walkamon self-hosted production

This directory contains the production runtime for:

- ASP.NET Core 8 API from `ghcr.io/walkamon/walkamon_backend:main`
- SQL Server 2022 Express with persistent host storage
- Cloudflare Tunnel for `https://api.walkamon.xyz`
- encrypted SQL backups to the private `walkamon-backups` R2 bucket

No public router port forwarding is required. SQL Server and the API bind only
to VM loopback; `cloudflared` reaches the API through the Docker `edge` network.

## 1. Windows and VMware

Install VMware Workstation Pro from the Broadcom Support Portal. A free basic
Broadcom account and acceptance of its download terms are required.

Create an Ubuntu Server 24.04 LTS VM with:

- 4 vCPU
- 6 GB RAM
- 40 GB dynamically allocated disk
- NIC 1: NAT
- NIC 2: Host-only network `192.168.120.0/24`
- Ubuntu hostname: `walkamon-prod`
- OpenSSH enabled; no desktop packages

After VMware is installed, an elevated PowerShell can create the VM definition
and thin disk with those settings. It verifies the official Ubuntu ISO checksum
and refuses to overwrite a non-empty target directory:

```powershell
.\deploy\windows\New-WalkamonVm.ps1 -ValidateOnly
.\deploy\windows\New-WalkamonVm.ps1
```

The installed VMware host-only adapter uses `192.168.120.1/24`. Inside Ubuntu, identify
the second NIC with `ip -br link`, then run:

```bash
sudo ./deploy/scripts/configure-hostonly-network.sh <host-only-interface> 192.168.120.10/24
sudo netplan try
```

Create a dedicated SSH key on Windows if one does not already exist, copy its
public half to the Ubuntu account, and verify a second key-based session before
disabling password authentication:

```powershell
ssh-keygen -t ed25519 -a 100 -f "$env:USERPROFILE\.ssh\walkamon_prod"
Get-Content "$env:USERPROFILE\.ssh\walkamon_prod.pub" |
  ssh <ubuntu-user>@192.168.120.10 `
  'umask 077; mkdir -p ~/.ssh; cat >> ~/.ssh/authorized_keys'
ssh -i "$env:USERPROFILE\.ssh\walkamon_prod" `
  <ubuntu-user>@192.168.120.10
```

From that verified SSH session, enable key-only authentication. The script
refuses to continue if the selected account has no `authorized_keys`:

```bash
sudo /opt/walkamon/scripts/harden-ssh.sh <ubuntu-user>
```

Keep the first session open until a new key-only session succeeds. Root login,
password authentication, and keyboard-interactive authentication are then
disabled. UFW permits SSH only from the Windows host-only address
`192.168.120.1`.

After the VM file exists, register headless startup from an elevated Windows
PowerShell:

```powershell
.\deploy\windows\Register-WalkamonVmStartup.ps1 `
  -VmxPath "C:\path\to\walkamon-prod.vmx"
```

The scheduled task starts the VM 60 seconds after Windows starts. The API is
offline whenever Windows, the VM, the router, or the Internet connection is
offline.

## 2. Ubuntu bootstrap and secrets

Clone the private repository into the VM and run:

```bash
sudo ./deploy/scripts/bootstrap-ubuntu.sh
```

Populate `/etc/walkamon/walkamon.env`. Keep it owned by root with mode `600`.
Values containing shell punctuation must be quoted. Generate the SQL/JWT/restic
passwords independently; use URL-safe random values without single quotes for
the SQL passwords.

```bash
sudo /opt/walkamon/scripts/initialize-production-secrets.sh
sudoedit /etc/walkamon/walkamon.env
sudo chmod 600 /etc/walkamon/walkamon.env
sudo install -o root -g 1654 -m 640 play-integrity.json \
  /etc/walkamon/secrets/play-integrity-service-account.json
sudo install -o root -g 1654 -m 640 firebase.json \
  /etc/walkamon/secrets/firebase-service-account.json
```

UID/GID `1654` is the fixed non-root API identity. The JSON credentials remain
read-only in the container and readable only by root plus that container group.
The environment file stays `root:root` mode `600`.

Before production, revoke and recreate the SQL `sa` password, JWT signing key,
Gmail App Password, Cloudinary secret, GHCR token, Cloudflare Tunnel token and
R2 token. Never copy the old values from Git history into production.

Run the preflight before the first start. It validates required values, secret
file ownership/JSON, Compose syntax, certificate format, and the 15 GB free
space floor without printing secret contents:

```bash
sudo /opt/walkamon/scripts/preflight.sh
```

## 3. Restore the database

The validated Windows migration backup is stored outside the repository under:

```text
C:\Users\tvhun\WalkamonBackups\Walkamon_migration_*.bak
```

Copy the newest file to the VM, start SQL Server, restore it, and provision the
least-privileged application login:

```bash
sudo docker compose \
  --env-file /etc/walkamon/walkamon.env \
  -f /opt/walkamon/compose.prod.yml up -d db

sudo /opt/walkamon/scripts/restore-database.sh ~/Walkamon_migration.bak
```

Expected migration baseline:

- database: `Walkamon`
- 50 user tables
- 80 rows at the verified migration snapshot
- 8 MB of SQL data files on the Windows source instance

The API uses `walkamon_app`, not `sa`. SQL Server is reachable from Windows
SSMS only through an SSH tunnel:

```powershell
ssh -L 14330:127.0.0.1:1433 <ubuntu-user>@192.168.120.10
```

Connect SSMS to `127.0.0.1,14330`.

Schema changes are a manual maintenance operation. The wrapper below first
creates a checksum-verified, encrypted R2 backup and only then executes the
approved SQL file with the administrative login:

```bash
sudo /opt/walkamon/scripts/maintenance-schema.sh \
  /path/to/approved-schema-change.sql
```

The application never receives `sa` credentials. A daily systemd check writes
a warning to the journal when SQL data files reach 7 GB, leaving time to move
off Express or split data before its 10 GB per-database data limit.

## 4. GHCR and automatic deployment

The GitHub workflow runs all tests on Windows because two integration suites
require LocalDB. A successful `main` build publishes:

```text
ghcr.io/walkamon/walkamon_backend:main
ghcr.io/walkamon/walkamon_backend:sha-<full-commit-sha>
```

Create a read-only fine-grained GitHub token for the VM with access only to the
Walkamon package and put it in `GHCR_TOKEN`. Then deploy once:

```bash
sudo /opt/walkamon/scripts/deploy.sh
sudo systemctl start walkamon-stack.service
sudo systemctl start walkamon-update.timer
```

The update timer checks every five minutes. It pulls `main`, starts the API,
waits for `/health/ready`, and restores the previous image if readiness fails.

## 5. Cloudflare

The `walkamon.xyz` zone uses the Free plan. Its authoritative nameservers are:

- `brian.ns.cloudflare.com`
- `jewel.ns.cloudflare.com`

The zone is Active, Universal SSL and Always Use HTTPS are enabled, and the
minimum TLS version is 1.2. HSTS remains disabled until cutover testing is
complete.

DNSSEC remains disabled because P.A Việt Nam currently requires a separate
paid registrar DNSSEC/DS service. It is optional for this beta deployment and
does not replace HTTPS, JWT validation, or application authorization.

The remotely managed Tunnel is named `walkamon-prod`. Complete the remaining
steps after the first connector is online:

1. Add public hostname `api.walkamon.xyz` with service
   `http://api:8080`.
2. Put the generated connector token in `CLOUDFLARE_TUNNEL_TOKEN`.
3. Add cache bypass rules for `/api/*`, `/hubs/*`, `/health/*`,
   and `/swagger/*`.
4. Protect `api.walkamon.xyz/swagger*` with Cloudflare Access email OTP.
5. Do not create any DNS/public hostname for SQL Server.

Enable HSTS only after HTTPS, API, WebSocket, and recovery testing succeeds.

## 6. R2 backups

Create a private bucket named `walkamon-backups`; public access must remain
disabled. Create an R2 S3 token scoped only to read/write that bucket and put
the endpoint and credentials in `/etc/walkamon/walkamon.env`.

```bash
sudo systemctl start walkamon-backup.service
sudo systemctl start walkamon-backup.timer
sudo systemctl start walkamon-restore-verify.timer
sudo systemctl start walkamon-db-size.timer
```

Nightly backups use SQL checksum plus `RESTORE VERIFYONLY`, then restic encrypts
and uploads them. Retention is 7 daily, 4 weekly, and 6 monthly snapshots. A
full temporary restore plus `DBCC CHECKDB` runs monthly. Store the restic
password separately in a password manager.

## 7. Acceptance checks

```bash
curl --fail http://127.0.0.1:8080/health/live
curl --fail http://127.0.0.1:8080/health/ready
docker compose --env-file /etc/walkamon/walkamon.env \
  -f /opt/walkamon/compose.prod.yml ps
systemctl list-timers 'walkamon-*'
```

Externally verify:

- `https://api.walkamon.xyz/health/live` returns HTTP 200.
- SignalR connects to `wss://api.walkamon.xyz/hubs/pvp-sprint`.
- Swagger requires Cloudflare Access authentication.
- no home router ports are forwarded;
- `1433` and `22` are not reachable from the Internet;
- turning the laptop off and on automatically restores the VM, stack, Tunnel,
  and latest healthy `main` image.
