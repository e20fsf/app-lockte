# PROTOTYPE — run on a dev PC (with .NET 10 SDK + internet). Produces report-pdf-proto.zip for the factory VM.
# The zip contains the published app + Chromium headless shell, so the VM needs no internet and no Playwright CLI.
param([string]$Out = "$PSScriptRoot\..\out")
$ErrorActionPreference = 'Stop'
$proj = Resolve-Path "$PSScriptRoot\.."
$pub = Join-Path $Out 'publish'
Remove-Item $Out -Recurse -Force -ErrorAction SilentlyContinue

dotnet publish $proj -c Release -r win-x64 --self-contained false -o $pub
if ($LASTEXITCODE) { throw 'publish failed' }

# Install Chromium headless shell into the publish folder (Program.cs points PLAYWRIGHT_BROWSERS_PATH there).
$env:PLAYWRIGHT_BROWSERS_PATH = Join-Path $pub 'ms-playwright'
& (Join-Path $pub 'playwright.ps1') install --only-shell chromium
if ($LASTEXITCODE) { throw 'playwright install failed' }

Copy-Item "$PSScriptRoot\Install-Proto.ps1", "$PSScriptRoot\Remove-Proto.ps1", "$PSScriptRoot\README.md" $Out

# Chromium has paths > 260 chars: Compress-Archive (PS 5.1) fails on them, Windows' built-in tar does not.
$zip = Join-Path (Split-Path $Out) 'report-pdf-proto.zip'
Remove-Item $zip -ErrorAction SilentlyContinue
tar -a -c -f $zip -C $Out .
if ($LASTEXITCODE) { throw 'zip failed' }
Write-Host "OK -> $zip"
