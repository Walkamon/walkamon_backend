[CmdletBinding()]
param(
    [string]$VmDirectory = "C:\VMs\walkamon-prod",

    [string]$IsoPath =
        "$env:USERPROFILE\Downloads\ubuntu-24.04.4-live-server-amd64.iso",

    [string]$VdiskManagerPath =
        "C:\Program Files\VMware\VMware Workstation\vmware-vdiskmanager.exe",

    [switch]$ValidateOnly
)

$ErrorActionPreference = "Stop"

$expectedIsoSha256 =
    "e907d92eeec9df64163a7e454cbc8d7755e8ddc7ed42f99dbc80c40f1a138433"
$resolvedIso = (Resolve-Path -LiteralPath $IsoPath).Path
$resolvedVdiskManager = (Resolve-Path -LiteralPath $VdiskManagerPath).Path
$resolvedVmDirectory = [System.IO.Path]::GetFullPath($VmDirectory)

$existingDirectory = Test-Path -LiteralPath $resolvedVmDirectory
if ($existingDirectory) {
    $existingFiles = Get-ChildItem -LiteralPath $resolvedVmDirectory -Force
    if ($existingFiles) {
        throw "Refusing to overwrite non-empty VM directory: $resolvedVmDirectory"
    }
}

$actualIsoSha256 =
    (Get-FileHash -LiteralPath $resolvedIso -Algorithm SHA256).Hash.ToLowerInvariant()
if ($actualIsoSha256 -ne $expectedIsoSha256) {
    throw "Ubuntu ISO checksum mismatch: $actualIsoSha256"
}

$freeBytes = (Get-PSDrive -Name ([System.IO.Path]::GetPathRoot($resolvedVmDirectory).TrimEnd(":\\"))).Free
if ($freeBytes -lt 20GB) {
    throw "At least 20 GB of free host storage is required before creating the thin VM disk."
}

$vmxPath = Join-Path $resolvedVmDirectory "walkamon-prod.vmx"
$vmdkPath = Join-Path $resolvedVmDirectory "walkamon-prod.vmdk"
$vmdkFileName = Split-Path -Leaf $vmdkPath
$isoForVmx = $resolvedIso.Replace("\", "/")

if ($ValidateOnly) {
    [pscustomobject]@{
        VmxPath = $vmxPath
        DiskPath = $vmdkPath
        DiskMaximumGB = 40
        MemoryMB = 6144
        Vcpus = 4
        IsoPath = $resolvedIso
        IsoVerified = $true
        ValidationOnly = $true
    }
    return
}

New-Item -ItemType Directory -Path $resolvedVmDirectory -Force | Out-Null

& $resolvedVdiskManager `
    -c `
    -s 40GB `
    -a lsilogic `
    -t 0 `
    $vmdkPath
if ($LASTEXITCODE -ne 0) {
    throw "vmware-vdiskmanager failed with exit code $LASTEXITCODE"
}

$vmx = @"
.encoding = "UTF-8"
config.version = "8"
virtualHW.version = "21"
displayName = "Walkamon Ubuntu Production"
guestOS = "ubuntu-64"
firmware = "efi"
memsize = "6144"
numvcpus = "4"
cpuid.coresPerSocket = "2"
mem.hotadd = "FALSE"
vcpu.hotadd = "FALSE"

pciBridge0.present = "TRUE"
pciBridge4.present = "TRUE"
pciBridge4.virtualDev = "pcieRootPort"
pciBridge4.functions = "8"
pciBridge5.present = "TRUE"
pciBridge5.virtualDev = "pcieRootPort"
pciBridge5.functions = "8"
pciBridge6.present = "TRUE"
pciBridge6.virtualDev = "pcieRootPort"
pciBridge6.functions = "8"
pciBridge7.present = "TRUE"
pciBridge7.virtualDev = "pcieRootPort"
pciBridge7.functions = "8"

scsi0.present = "TRUE"
scsi0.virtualDev = "lsilogic"
scsi0:0.present = "TRUE"
scsi0:0.fileName = "$vmdkFileName"

sata0.present = "TRUE"
sata0:0.present = "TRUE"
sata0:0.deviceType = "cdrom-image"
sata0:0.fileName = "$isoForVmx"
sata0:0.startConnected = "TRUE"

ethernet0.present = "TRUE"
ethernet0.connectionType = "nat"
ethernet0.virtualDev = "e1000e"
ethernet0.addressType = "generated"

ethernet1.present = "TRUE"
ethernet1.connectionType = "hostonly"
ethernet1.vnet = "VMnet1"
ethernet1.virtualDev = "e1000e"
ethernet1.addressType = "generated"

sound.present = "FALSE"
printer.present = "FALSE"
serial0.present = "FALSE"
parallel0.present = "FALSE"
usb.present = "FALSE"
usb_xhci.present = "FALSE"
floppy0.present = "FALSE"

tools.syncTime = "TRUE"
powerType.powerOff = "soft"
powerType.powerOn = "soft"
powerType.suspend = "soft"
powerType.reset = "soft"
"@

Set-Content -LiteralPath $vmxPath -Value $vmx -Encoding UTF8

[pscustomobject]@{
    VmxPath = $vmxPath
    DiskPath = $vmdkPath
    DiskMaximumGB = 40
    MemoryMB = 6144
    Vcpus = 4
    IsoPath = $resolvedIso
    IsoVerified = $true
}
