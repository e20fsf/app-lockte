# PROTOTYPE — run on the factory VM (Windows Server 2025) in an elevated PowerShell, from the unzipped folder.
# Creates an IIS site on port 8099 under its own app pool, renders the test PDFs through IIS, and collects
# results into .\results (zip it and send back). Remove everything afterwards with Remove-Proto.ps1.
param([int]$Port = 8099, [string]$Root = 'C:\inetpub\report-pdf-proto')
$ErrorActionPreference = 'Stop'
$Name = 'ReportPdfProto'
$results = Join-Path $PSScriptRoot 'results'
New-Item -ItemType Directory -Force $results | Out-Null
Start-Transcript (Join-Path $results 'install-log.txt') -Force | Out-Null

# 1. Prerequisites (installed by the admin per ADR 0001 — this script does not install them)
if (-not (Get-Service W3SVC -ErrorAction SilentlyContinue)) { throw 'IIS (Web-Server role) is not installed.' }
if (-not (Test-Path "$env:ProgramFiles\IIS\Asp.Net Core Module\V2\aspnetcorev2.dll")) {
    throw 'ASP.NET Core Hosting Bundle (.NET 10) is not installed: https://dotnet.microsoft.com/download/dotnet/10.0'
}
Import-Module WebAdministration

# 2. Copy app (publish folder includes ms-playwright\chromium_headless_shell-*)
if (Test-Path $Root) { throw "$Root already exists — run Remove-Proto.ps1 first." }
robocopy (Join-Path $PSScriptRoot 'publish') $Root /E /NFL /NDL /NJH /NJS | Out-Null  # long Chromium paths
if ($LASTEXITCODE -ge 8) { throw "copy failed (robocopy $LASTEXITCODE)" }

# 3. App pool: no managed code (ASP.NET Core), load user profile so Chromium gets a TEMP/profile dir, never idle out.
$pool = New-WebAppPool $Name
Set-ItemProperty "IIS:\AppPools\$Name" managedRuntimeVersion ''
Set-ItemProperty "IIS:\AppPools\$Name" processModel.loadUserProfile $true
Set-ItemProperty "IIS:\AppPools\$Name" processModel.idleTimeout ([TimeSpan]::Zero)
New-Website -Name $Name -Port $Port -PhysicalPath $Root -ApplicationPool $Name | Out-Null
# App-pool identity needs read+execute on the folder (Chromium exe lives inside it).
icacls $Root /grant "IIS AppPool\${Name}:(OI)(CI)RX" | Out-Null

# 4. Exercise it through IIS and collect evidence
$base = "http://localhost:$Port"
$sw = [Diagnostics.Stopwatch]::StartNew()
Invoke-WebRequest "$base/env" -UseBasicParsing -TimeoutSec 300 -OutFile "$results\env.json"
"first request (app start + Chromium warm-up): $($sw.ElapsedMilliseconds) ms" | Tee-Object "$results\timings.txt"
foreach ($t in @(@{u='kr2?i=0'; f='kr2.pdf'}, @{u='register'; f='register-450.pdf'}, @{u='register?rows=1200'; f='register-1200.pdf'}, @{u='thai'; f='thai.pdf'})) {
    $r = Invoke-WebRequest "$base/$($t.u)" -UseBasicParsing -TimeoutSec 300 -OutFile "$results\$($t.f)" -PassThru
    "$($t.f): html=$($r.Headers['X-Html-Ms']) ms, pdf=$($r.Headers['X-Pdf-Ms']) ms, size=$((Get-Item "$results\$($t.f)").Length) bytes" | Tee-Object "$results\timings.txt" -Append
}
Invoke-WebRequest "$base/bench?n=5&parallel=5" -UseBasicParsing -TimeoutSec 600 -OutFile "$results\bench.json"
Invoke-WebRequest "$base/env" -UseBasicParsing -OutFile "$results\env-after.json"
Get-CimInstance Win32_OperatingSystem | Select-Object Caption, Version, TotalVisibleMemorySize, FreePhysicalMemory | Format-List | Out-File "$results\machine.txt"
Stop-Transcript | Out-Null
Compress-Archive "$results\*" (Join-Path $PSScriptRoot 'results.zip') -Force
Write-Host "Done. Send back: $(Join-Path $PSScriptRoot 'results.zip')  — then run Remove-Proto.ps1" -ForegroundColor Green
