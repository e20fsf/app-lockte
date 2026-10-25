# Run on the server as Administrator: removes the IIS site, its app pool and the firewall rule made by
# Install-Lockte.ps1. Files and the database stay unless asked: -RemoveFiles deletes <InstallRoot> (settings and logs
# included); the database is never dropped here (do that in SQL Server Management Studio if really meant).
param(
    [string]$SiteName = 'Lockte',
    [int]$Port = 8080,
    [string]$InstallRoot = 'C:\Lockte',
    [switch]$RemoveFiles
)
$ErrorActionPreference = 'Stop'
Import-Module WebAdministration
if (Get-Website $SiteName) { Remove-Website $SiteName; Write-Host "removed site $SiteName" }
if (Test-Path "IIS:\AppPools\$SiteName") { Remove-WebAppPool $SiteName; Write-Host "removed app pool $SiteName" }
Remove-NetFirewallRule -Name "Lockte-$Port" -ErrorAction SilentlyContinue
if ($RemoveFiles -and (Test-Path $InstallRoot)) {
    Start-Sleep 3   # let w3wp let go of the files
    Remove-Item $InstallRoot -Recurse -Force
    Write-Host "removed $InstallRoot"
}
