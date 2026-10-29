# Run on a dev PC (.NET 10 SDK + internet). Builds lockte-<version>.zip for the server: the published web app with
# Chromium headless shell inside it, so the server needs no internet, no SDK and no Playwright CLI (#21).
#   .\deploy\Build-Package.ps1 [-HostingBundle <path to dotnet-hosting-10.x-win.exe>] [-AppOnly]
# -AppOnly leaves Chromium out (a few MB instead of ~170): for updating a server that already runs Lockte (Install-Lockte.ps1 keeps its browser).
# -HostingBundle puts the ASP.NET Core Hosting Bundle in the zip (prereqs\) so Install-Lockte.ps1 can install it offline.
param(
    [string]$Out = (Join-Path $PSScriptRoot '..\out\deploy'),
    [string]$HostingBundle,
    [switch]$AppOnly
)
$ErrorActionPreference = 'Stop'
$repo = Resolve-Path (Join-Path $PSScriptRoot '..')
$version = '{0}-{1}' -f (Get-Date -Format 'yyyyMMdd'), (git -C $repo rev-parse --short HEAD)
if (git -C $repo status --porcelain --untracked-files=no) { $version += '-dirty' }
if ($AppOnly) { $version += '-app-only' }

Remove-Item $Out -Recurse -Force -ErrorAction SilentlyContinue
$app = Join-Path $Out 'app'
dotnet publish (Join-Path $repo 'src\Lockte.Web\Lockte.Web.csproj') -c Release -r win-x64 --self-contained false -o $app
if ($LASTEXITCODE) { throw 'dotnet publish failed' }
# Never ship a developer's settings: production settings live on the server (config\appsettings.Production.json).
Remove-Item (Join-Path $app 'appsettings.Development.json') -ErrorAction SilentlyContinue

# Chromium into the app folder; the installer points Reports:BrowsersPath there (the app pool has no usable profile).
if (-not $AppOnly) {
    $env:PLAYWRIGHT_BROWSERS_PATH = Join-Path $app 'ms-playwright'
    & (Join-Path $app 'playwright.ps1') install --only-shell chromium
    if ($LASTEXITCODE) { throw 'playwright install failed' }
}

Copy-Item (Join-Path $PSScriptRoot 'Install-Lockte.ps1'), (Join-Path $PSScriptRoot 'Remove-Lockte.ps1'), (Join-Path $PSScriptRoot 'README.md') $Out
if ($HostingBundle) {
    New-Item -ItemType Directory (Join-Path $Out 'prereqs') | Out-Null
    Copy-Item $HostingBundle (Join-Path $Out 'prereqs')
}
Set-Content (Join-Path $Out 'version.txt') $version

# Chromium has paths over 260 characters: Compress-Archive (PowerShell 5.1) fails on them, Windows' tar does not.
$zip = Join-Path (Split-Path $Out) "lockte-$version.zip"
Remove-Item $zip -ErrorAction SilentlyContinue
tar -a -c -f $zip -C $Out .
if ($LASTEXITCODE) { throw 'zip failed' }
Write-Host "OK -> $zip" -ForegroundColor Green
