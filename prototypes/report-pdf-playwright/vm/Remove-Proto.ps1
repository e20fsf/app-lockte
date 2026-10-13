# PROTOTYPE — removes everything Install-Proto.ps1 created. Run elevated on the VM.
param([string]$Root = 'C:\inetpub\report-pdf-proto')
$Name = 'ReportPdfProto'
Import-Module WebAdministration
if (Get-Website $Name) { Remove-Website $Name }
if (Test-Path "IIS:\AppPools\$Name") { Stop-WebAppPool $Name -ErrorAction SilentlyContinue; Start-Sleep 2; Remove-WebAppPool $Name }
if (Test-Path $Root) { Remove-Item $Root -Recurse -Force }
Write-Host 'Removed.'
