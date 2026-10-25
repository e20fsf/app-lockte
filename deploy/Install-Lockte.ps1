# Run on the server (Windows Server 2025) in PowerShell "Run as Administrator", from the unzipped package folder.
# Installs or updates the Lockte web app on IIS, checks that reports print under the app pool (#21) and writes
# results-<time>.zip (no passwords in it) to send back to the developers. See README.md.
#   First install:  .\Install-Lockte.ps1 -SqlServer <server> -Database Lockte -SqlUser <login> -ResetAdmin
#   Update:         .\Install-Lockte.ps1
#   New database:   .\Install-Lockte.ps1 -Database <name> [-SqlUser <login>] -ResetSettings   (old settings kept as .bak)
param(
    [ValidatePattern('^[A-Za-z0-9_-]+$')][string]$SiteName = 'Lockte',
    [int]$Port = 8080,
    [string]$InstallRoot = 'C:\Lockte',
    [string]$SqlServer = 'localhost',
    [ValidatePattern('^[A-Za-z0-9_-]+$')][string]$Database = 'Lockte',
    [string]$SqlUser,                    # blank = Windows authentication as the app pool (IIS AppPool\<SiteName>)
    [switch]$ResetSettings,              # write the settings file again from the parameters above
    [switch]$ResetAdmin,                 # create the admin account / give it a temporary password (asks for it)
    [switch]$SkipWindowsFeatures
)
$ErrorActionPreference = 'Stop'
# IIS: drive (WebAdministration) and Get-WindowsFeature work reliably only in Windows PowerShell 5.1.
if ($PSVersionTable.PSEdition -eq 'Core') { throw 'Run this in "Windows PowerShell" (5.1, powershell.exe), not PowerShell 7 (pwsh.exe).' }
$identity =[Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
if (-not $identity.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Run PowerShell as Administrator.' }
$askedForDatabase = $PSBoundParameters.ContainsKey('SqlServer') -or $PSBoundParameters.ContainsKey('Database') -or $PSBoundParameters.ContainsKey('SqlUser')

$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$appDir = Join-Path $InstallRoot 'app'
$configDir = Join-Path $InstallRoot 'config'
$logDir = Join-Path $InstallRoot 'logs'
$results = Join-Path $InstallRoot "results-$stamp"
$settingsFile = Join-Path $configDir 'appsettings.Production.json'
$poolUser = "IIS AppPool\$SiteName"
New-Item -ItemType Directory -Force $appDir, $configDir, $logDir, $results | Out-Null
Start-Transcript (Join-Path $results 'install-log.txt') -Force | Out-Null
$version = Get-Content (Join-Path $PSScriptRoot 'version.txt') -ErrorAction SilentlyContinue
Write-Host "Lockte $version -> $InstallRoot (site $SiteName, port $Port)"

function Step($text) { Write-Host "`n== $text" -ForegroundColor Cyan }

try {
    Step '1. Windows features: IIS + WebSocket (Blazor Server needs it) + Application Initialization (start with IIS)'
    # Get-WindowsFeature lives in module ServerManager (Windows PowerShell 5.1 on Windows Server); in PowerShell 7 it
    # may not load, then only check that IIS is there.
    Import-Module ServerManager -ErrorAction SilentlyContinue
    if (-not $SkipWindowsFeatures -and -not (Get-Command Get-WindowsFeature -ErrorAction SilentlyContinue)) {
        Write-Host 'Get-WindowsFeature is not available in this PowerShell (use Windows PowerShell 5.1 to install features); checking IIS only' -ForegroundColor Yellow
        $SkipWindowsFeatures = $true
    }
    if ($SkipWindowsFeatures -and -not (Get-Service W3SVC -ErrorAction SilentlyContinue)) { throw 'IIS (Web-Server role) is not installed.' }
    if (-not $SkipWindowsFeatures) {
        $features = 'Web-Server', 'Web-WebSockets', 'Web-AppInit', 'Web-Mgmt-Console'
        $missing = Get-WindowsFeature $features | Where-Object { -not $_.Installed }
        if ($missing) {
            Install-WindowsFeature ($missing.Name) | Out-Null
            Write-Host "installed: $($missing.Name -join ', ')"
        } else { Write-Host 'already installed' }
    }

    Step '2. ASP.NET Core Hosting Bundle .NET 10'
    $ancm = "$env:ProgramFiles\IIS\Asp.Net Core Module\V2\aspnetcorev2.dll"
    $hasRuntime = { (& "$env:ProgramFiles\dotnet\dotnet.exe" --list-runtimes 2>$null) -match '^Microsoft\.AspNetCore\.App 10\.' }
    if (-not (Test-Path $ancm) -or -not (Test-Path "$env:ProgramFiles\dotnet\dotnet.exe") -or -not (& $hasRuntime)) {
        $bundle = Get-ChildItem (Join-Path $PSScriptRoot 'prereqs') -Filter 'dotnet-hosting-10*.exe' -ErrorAction SilentlyContinue | Select-Object -First 1
        if (-not $bundle) {
            throw 'Hosting Bundle .NET 10 is not installed. Download "Hosting Bundle" from https://dotnet.microsoft.com/download/dotnet/10.0, put it in prereqs\ (or install it) and run again.'
        }
        $p = Start-Process $bundle.FullName '/install /quiet /norestart' -Wait -PassThru
        if ($p.ExitCode -notin 0, 3010) { throw "Hosting Bundle install failed (exit $($p.ExitCode))" }
        net stop was /y | Out-Null; net start w3svc | Out-Null
        Write-Host "installed $($bundle.Name)"
    } else { Write-Host 'already installed' }
    Import-Module WebAdministration

    Step '3. Settings (kept between updates, never in the package)'
    if ($ResetSettings -and (Test-Path $settingsFile)) {
        Move-Item $settingsFile "$settingsFile.bak-$stamp"
        Write-Host "old settings moved to $settingsFile.bak-$stamp"
    }
    if (-not (Test-Path $settingsFile)) {
        $cs = New-Object System.Data.SqlClient.SqlConnectionStringBuilder
        $cs['Data Source'] = $SqlServer
        $cs['Initial Catalog'] = $Database
        $cs['TrustServerCertificate'] = $true
        if ($SqlUser) {
            $secure = Read-Host "Password of SQL login '$SqlUser'" -AsSecureString
            $cs['User ID'] = $SqlUser
            $cs['Password'] = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure))
        } else { $cs['Integrated Security'] = $true }
        $settings = [ordered]@{
            ConnectionStrings = [ordered]@{ Lockte = $cs.ConnectionString }
            Reports = [ordered]@{ BrowsersPath = (Join-Path $appDir 'ms-playwright'); InstallBrowser = $false }
        }
        $settings | ConvertTo-Json -Depth 5 | Set-Content $settingsFile -Encoding UTF8
        Write-Host "written $settingsFile (edit it for Employer:* names and addresses, then run this script again)"
    } else { Write-Host "kept $settingsFile" }
    # Which database the app will use (never the password): an old settings file wins over the parameters.
    $db = New-Object System.Data.SqlClient.SqlConnectionStringBuilder ((Get-Content $settingsFile -Raw | ConvertFrom-Json).ConnectionStrings.Lockte)
    $windowsAuth = [bool]$db['Integrated Security']
    $login = if ($windowsAuth) { "Windows: $poolUser" } else { "SQL login: $($db['User ID'])" }
    Write-Host "database: server $($db['Data Source']), database $($db['Initial Catalog']), $login"
    if ($askedForDatabase -and ($db['Data Source'] -ne $SqlServer -or $db['Initial Catalog'] -ne $Database -or [string]$db['User ID'] -ne [string]$SqlUser)) {
        throw "The settings file points to another database than the parameters. Run again with -ResetSettings to write it from the parameters (the old file is kept as .bak), or without -SqlServer/-Database/-SqlUser to keep it."
    }
    # Only administrators and the app pool may read it: it holds the database password.
    icacls $configDir /inheritance:r /grant:r 'BUILTIN\Administrators:(OI)(CI)F' 'NT AUTHORITY\SYSTEM:(OI)(CI)F' | Out-Null

    Step '4. App pool and site'
    if (-not (Test-Path "IIS:\AppPools\$SiteName")) { New-WebAppPool $SiteName | Out-Null; Write-Host "created app pool $SiteName" }
    $pool = "IIS:\AppPools\$SiteName"
    Set-ItemProperty $pool managedRuntimeVersion ''                       # ASP.NET Core: no managed code
    Set-ItemProperty $pool startMode 'AlwaysRunning'                      # start (and warm Chromium) with IIS
    Set-ItemProperty $pool processModel.idleTimeout ([TimeSpan]::Zero)    # never stop when idle
    Set-ItemProperty $pool processModel.loadUserProfile $true             # a TEMP folder for Chromium
    if ((Get-WebAppPoolState $SiteName).Value -eq 'Started') { Stop-WebAppPool $SiteName; Start-Sleep 5 }
    icacls $configDir /grant "${poolUser}:(OI)(CI)R" | Out-Null

    if ($windowsAuth) {
        Step '4b. SQL Server access for the app pool (Windows authentication)'
        # The app pool's virtual account only exists on this machine, so this works when SQL Server runs here too.
        # Done as the administrator running this script (a sysadmin of SQL Server).
        $admin = New-Object System.Data.SqlClient.SqlConnectionStringBuilder
        $admin['Data Source'] = $db['Data Source']; $admin['Initial Catalog'] = 'master'
        $admin['Integrated Security'] = $true; $admin['TrustServerCertificate'] = $true
        $dbName = $db['Initial Catalog']
        $sql = @"
IF SUSER_ID(N'$poolUser') IS NULL CREATE LOGIN [$poolUser] FROM WINDOWS;
IF DB_ID(N'$dbName') IS NULL CREATE DATABASE [$dbName];
EXEC (N'USE [$dbName];
IF USER_ID(N''$poolUser'') IS NULL CREATE USER [$poolUser] FOR LOGIN [$poolUser];
ALTER ROLE db_owner ADD MEMBER [$poolUser];');
"@
        $conn = New-Object System.Data.SqlClient.SqlConnection $admin.ConnectionString
        try {
            $conn.Open()
            $cmd = $conn.CreateCommand(); $cmd.CommandText = $sql; [void]$cmd.ExecuteNonQuery()
            Write-Host "$poolUser may use database $dbName (db_owner)"
        }
        catch { throw "Could not give $poolUser access to $dbName on $($db['Data Source']): $($_.Exception.Message) - or use -SqlUser with -ResetSettings." }
        finally { $conn.Dispose() }
    }

    Step '5. Copy the app'
    robocopy (Join-Path $PSScriptRoot 'app') $appDir /MIR /NFL /NDL /NJH /NJS /NP /XF appsettings.Production.json | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "copy failed (robocopy $LASTEXITCODE)" }
    $appSettings = Join-Path $appDir 'appsettings.Production.json'
    Copy-Item $settingsFile $appSettings -Force
    icacls $appSettings /inheritance:r /grant:r 'BUILTIN\Administrators:F' 'NT AUTHORITY\SYSTEM:F' "${poolUser}:R" | Out-Null
    # stdout of the app (its log) into logs\ - the first place to look when the site does not start.
    $webConfig = Join-Path $appDir 'web.config'
    [xml]$xml = Get-Content $webConfig
    $core = $xml.SelectSingleNode('//aspNetCore')
    $core.SetAttribute('stdoutLogEnabled', 'true')
    $core.SetAttribute('stdoutLogFile', (Join-Path $logDir 'stdout'))
    $xml.Save($webConfig)
    icacls $appDir /grant "${poolUser}:(OI)(CI)RX" | Out-Null
    icacls $logDir /grant "${poolUser}:(OI)(CI)M" | Out-Null

    if (-not (Get-Website $SiteName)) {
        New-Website -Name $SiteName -Port $Port -PhysicalPath $appDir -ApplicationPool $SiteName | Out-Null
        Write-Host "created site $SiteName on port $Port"
    }
    Set-ItemProperty "IIS:\Sites\$SiteName" applicationDefaults.preloadEnabled $true
    if (-not (Get-NetFirewallRule -Name "Lockte-$Port" -ErrorAction SilentlyContinue)) {
        New-NetFirewallRule -Name "Lockte-$Port" -DisplayName "Lockte web ($Port)" -Direction Inbound -Protocol TCP -LocalPort $Port -Action Allow | Out-Null
    }

    if ($ResetAdmin) {
        Step '6. admin account (temporary password, changed at the first login)'
        Push-Location $appDir
        $env:ASPNETCORE_ENVIRONMENT = 'Production'
        & "$env:ProgramFiles\dotnet\dotnet.exe" Lockte.Web.dll reset-admin
        $code = $LASTEXITCODE
        Pop-Location
        if ($code) { throw 'reset-admin failed (see above)' }
    }

    Step '7. Start and check'
    Start-WebAppPool $SiteName
    Start-Website $SiteName -ErrorAction SilentlyContinue
    $base = "http://localhost:$Port"
    $watch = [Diagnostics.Stopwatch]::StartNew()
    $page = $null
    $lastLog = { Get-ChildItem $logDir -Filter 'stdout_*.log' | Sort-Object LastWriteTime | Select-Object -Last 1 }
    while (-not $page -and $watch.Elapsed.TotalSeconds -lt 180) {
        try { $page = Invoke-WebRequest "$base/Account/Login" -UseBasicParsing -TimeoutSec 30 }
        catch {
            # 500.30 = the app stopped while starting: no point waiting, show why.
            if ($_.Exception.Response -and [int]$_.Exception.Response.StatusCode -eq 500) {
                $log = & $lastLog
                if ($log) { Write-Host "`n--- end of $($log.FullName):" -ForegroundColor Yellow; Get-Content $log.FullName -Tail 15 | Write-Host }
                throw "the app failed to start (HTTP 500.30) - see the log above"
            }
            Start-Sleep 3
        }
    }
    if (-not $page) { throw "the site did not answer in 3 minutes - see $logDir\stdout_*.log" }
    "login page: HTTP $($page.StatusCode) after $($watch.ElapsedMilliseconds) ms" | Tee-Object (Join-Path $results 'checks.txt')
    # Chromium warms up in the background (~30 s on the VM); the first print waits for it.
    function Health {
        try { (Invoke-WebRequest "$base/health/reports" -UseBasicParsing -TimeoutSec 300).Content }
        catch { if ($_.ErrorDetails.Message) { $_.ErrorDetails.Message } else { $_.Exception.Message } }
    }
    $first = Health
    "report print under the app pool (first): $first" | Tee-Object (Join-Path $results 'checks.txt') -Append
    $ok = $first -match '"ok":true'
    if ($ok) {
        "report print (warm, 3 times): $((1..3 | ForEach-Object { Health }) -join ' ')" | Tee-Object (Join-Path $results 'checks.txt') -Append
        Invoke-WebRequest "$base/health/reports?pdf=1" -UseBasicParsing -TimeoutSec 120 -OutFile (Join-Path $results 'health-reports.pdf')
    }
}
catch {
    $ok = $false
    Write-Host "FAILED: $_" -ForegroundColor Red
}
finally {
    # Evidence for the developers - never the settings file (database password).
    Get-CimInstance Win32_OperatingSystem | Select-Object Caption, Version, TotalVisibleMemorySize, FreePhysicalMemory | Format-List | Out-File (Join-Path $results 'machine.txt')
    & "$env:ProgramFiles\dotnet\dotnet.exe" --list-runtimes 2>$null | Out-File (Join-Path $results 'runtimes.txt')
    Get-Process w3wp, chrome-headless-shell -ErrorAction SilentlyContinue | Select-Object Name, Id, @{n = 'MB'; e = { [int]($_.WorkingSet64 / 1MB) } } |
        Format-Table | Out-File (Join-Path $results 'memory.txt')
    Get-ChildItem $logDir -Filter 'stdout_*.log' | Sort-Object LastWriteTime | Select-Object -Last 2 | Copy-Item -Destination $results
    Stop-Transcript | Out-Null
    $zip = "$results.zip"
    Compress-Archive "$results\*" $zip -Force
    if ($ok) { Write-Host "`nOK - open http://<server>:$Port  -  send back $zip" -ForegroundColor Green }
    else { Write-Host "`nNot finished - send back $zip" -ForegroundColor Yellow }
}
