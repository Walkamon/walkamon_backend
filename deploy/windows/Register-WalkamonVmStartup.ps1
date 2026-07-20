param(
    [Parameter(Mandatory = $true)]
    [string]$VmxPath,

    [string]$VmrunPath = "C:\Program Files\VMware\VMware Workstation\vmrun.exe"
)

$ErrorActionPreference = "Stop"

$resolvedVmx = (Resolve-Path -LiteralPath $VmxPath).Path
$resolvedVmrun = (Resolve-Path -LiteralPath $VmrunPath).Path
$taskName = "Walkamon Ubuntu VM"

$action = New-ScheduledTaskAction `
    -Execute $resolvedVmrun `
    -Argument "-T ws start `"$resolvedVmx`" nogui"
$trigger = New-ScheduledTaskTrigger -AtStartup
$trigger.Delay = "PT60S"
$settings = New-ScheduledTaskSettingsSet `
    -AllowStartIfOnBatteries `
    -DontStopIfGoingOnBatteries `
    -ExecutionTimeLimit ([TimeSpan]::Zero) `
    -RestartCount 3 `
    -RestartInterval (New-TimeSpan -Minutes 2)

Register-ScheduledTask `
    -TaskName $taskName `
    -Action $action `
    -Trigger $trigger `
    -Settings $settings `
    -User "SYSTEM" `
    -RunLevel Highest `
    -Force

Write-Host "Registered '$taskName'. The VM will start headless about 60 seconds after Windows boots."
