<#
.SYNOPSIS
  สร้างหรือรีเซ็ตฐานข้อมูลทดสอบ UAT (#134) จากไฟล์ Access ข้อมูลปลอมด้วยคำสั่งเดียว.

.DESCRIPTION
  ลบฐาน -Database ถ้ามี -> สร้างโครงใหม่ด้วยเว็บแอป (migrate) -> ย้ายข้อมูลทั้งชุด (พนักงาน, ผลผลิตและORDER) จากไฟล์ที่ระบุ.
  ใช้กับฐานทดสอบเท่านั้น: ชื่อฐานต้องลงท้ายด้วย Uat / Test / Dev และห้ามเป็น 'Lockte' และห้ามมีธงเปิดใช้งานจริง (Lockte.GoLive).
  ใช้ไฟล์ข้อมูลปลอมเท่านั้น (ชื่อไฟล์ต้องมีคำว่า fake) — ห้ามนำข้อมูลจริงขึ้นเซิร์ฟเวอร์ที่ลูกค้าเข้าถึงได้.
  เฉพาะสเปคตรวจ QA (ข้อมูลจริงของบริษัท) ไม่ถูกโหลดในสคริปต์นี้.

  connection string ของเซิร์ฟเวอร์ (ไม่ใส่ Database) อ่านจาก env LOCKTE_UAT_SQL — ไม่รับเป็นพารามิเตอร์ ไม่เขียนลงไฟล์ ไม่แสดงในผลลัพธ์.

.EXAMPLE
  $env:LOCKTE_UAT_SQL = 'Server=...;User Id=...;Password=...;TrustServerCertificate=True'
  .\tools\uat\Reset-Uat.ps1 -Database LockteUat -Employee .\source\samples\พนักงาน\work\พนักงาน-fake.accdb `
      -Order '.\source\samples\ผลผลิตและORDER\ผลผลิตและORDER_fake.accdb' -AdminPassword 'ตั้งรหัสชั่วคราวเอง' -Out $env:TEMP\uat-report
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Database,
    [Parameter(Mandatory)] [string] $Employee,
    [Parameter(Mandatory)] [string] $Order,
    [Parameter(Mandatory)] [string] $AdminPassword,
    [string] $Out = (Join-Path $env:TEMP 'lockte-uat-report'),
    [switch] $RemoveStockDuplicates,
    [string] $CodeMap
)
$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..\..')

if ($Database -notmatch '^Lockte\w*(Uat|Test|Dev)$' -or $Database -eq 'Lockte') { throw "ชื่อฐาน '$Database' ไม่ใช่ฐานทดสอบ (ต้องขึ้นต้น Lockte และลงท้าย Uat/Test/Dev)" }
foreach ($file in @($Employee, $Order)) {
    if (-not (Test-Path $file)) { throw "ไม่พบไฟล์ $file" }
    if ((Split-Path $file -Leaf) -notmatch 'fake') { throw "ไฟล์ '$file' ไม่ใช่ไฟล์ข้อมูลปลอม (ชื่อต้องมี fake) — ห้ามใช้ข้อมูลจริงกับฐานทดสอบ" }
}
$server = $env:LOCKTE_UAT_SQL
if ([string]::IsNullOrWhiteSpace($server)) { throw 'ตั้ง env LOCKTE_UAT_SQL (connection string ของเซิร์ฟเวอร์ ไม่ใส่ Database) ก่อน' }
$server = $server.TrimEnd(';')
$connection = "$server;Database=$Database"

function Invoke-Sql([string] $cs, [string] $sql) {
    $c = New-Object System.Data.SqlClient.SqlConnection $cs
    $c.Open()
    try { $cmd = $c.CreateCommand(); $cmd.CommandText = $sql; $cmd.CommandTimeout = 120; return $cmd.ExecuteScalar() } finally { $c.Dispose() }
}

$watch = [Diagnostics.Stopwatch]::StartNew()
Write-Host "1/4 ตรวจธงเปิดใช้งานจริงและลบฐาน $Database ถ้ามี"
$exists = Invoke-Sql "$server;Database=master" "SELECT COUNT(*) FROM sys.databases WHERE name = N'$Database'"
if ($exists -gt 0) {
    $live = Invoke-Sql $connection "SELECT COUNT(*) FROM sys.extended_properties WHERE class = 0 AND name = N'Lockte.GoLive'"
    if ($live -gt 0) { throw "ฐาน $Database ตั้งธงเปิดใช้งานจริงแล้ว (Lockte.GoLive) — ไม่รีเซ็ต" }
    Invoke-Sql "$server;Database=master" "ALTER DATABASE [$Database] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$Database]" | Out-Null
}

Write-Host '2/4 สร้างโครงฐานข้อมูล (เว็บแอป migrate)'
$env:ConnectionStrings__Lockte = $connection
$env:Reports__WarmUpOnStart = 'false'
dotnet run --project (Join-Path $root 'src\Lockte.Web') -c Release -- reset-admin --password $AdminPassword | Out-Host
if ($LASTEXITCODE -ne 0) { throw 'สร้างโครงฐานข้อมูลไม่สำเร็จ' }

Write-Host '3/4 ย้ายข้อมูลทั้งชุดจากไฟล์ข้อมูลปลอม'
$arguments = @('--project', (Join-Path $root 'src\Lockte.Migration'), '-c', 'Release', '--', '--program', 'all', '--employee', (Resolve-Path $Employee), '--order', (Resolve-Path $Order),
    '--connection', $connection, '--confirm', $Database, '--out', $Out)
if ($RemoveStockDuplicates) { $arguments += @('--remove-stock-duplicates', 'yes') }
if ($CodeMap) { $arguments += @('--map', (Resolve-Path $CodeMap)) }
dotnet run @arguments | Out-Host
if ($LASTEXITCODE -ne 0) { Write-Warning 'มีตารางที่นับไม่ตรง — ดูรายงานก่อนใช้ฐานนี้' }

Write-Host ("4/4 เสร็จใน {0:0.0} นาที — รายงาน: {1}" -f $watch.Elapsed.TotalMinutes, (Join-Path $Out 'summary.md'))
