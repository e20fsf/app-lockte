<#
.SYNOPSIS
  สร้างหรือรีเซ็ตฐานข้อมูลทดสอบของโปรแกรมวัตถุดิบด้วยข้อมูลปลอมชุดเดียว (#269) ด้วยคำสั่งเดียว.

.DESCRIPTION
  ลบฐาน -Database ถ้ามี -> สร้างโครงใหม่ด้วยเว็บแอป (migrate + ตั้งรหัส admin) -> เติมข้อมูลปลอมของวัตถุดิบ (Lockte.Web seed-purch-fake).
  ใช้กับฐานทดสอบเท่านั้น: ชื่อฐานต้องขึ้นต้น Lockte ลงท้าย Uat / Test / Dev และห้ามเป็น 'Lockte' และห้ามมีธงเปิดใช้งานจริง (Lockte.GoLive).
  ข้อมูลทั้งหมดสร้างจากสคริปต์ (seed คงที่) ไม่อ่านไฟล์ Access หรือไฟล์ใด ๆ จึงไม่มีข้อมูลจริง.
  ฐานนี้มีแต่ข้อมูลวัตถุดิบ (และแผนกทดสอบถ้ายังไม่มี) — ใช้ชื่อฐานแยกจาก LockteUat ที่มีข้อมูลพนักงาน/ORDER.

  connection string ของเซิร์ฟเวอร์ (ไม่ใส่ Database) อ่านจาก env LOCKTE_UAT_SQL — ไม่รับเป็นพารามิเตอร์ ไม่เขียนลงไฟล์ ไม่แสดงในผลลัพธ์.

.EXAMPLE
  $env:LOCKTE_UAT_SQL = 'Server=...;User Id=...;Password=...;TrustServerCertificate=True'
  .\tools\uat\Reset-PurchFake.ps1 -Database LockteMatUat -AdminPassword 'ตั้งรหัสชั่วคราวเอง'
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Database,
    [Parameter(Mandatory)] [string] $AdminPassword
)
$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..\..')

if ($Database -notmatch '^Lockte\w*(Uat|Test|Dev)$' -or $Database -eq 'Lockte') { throw "ชื่อฐาน '$Database' ไม่ใช่ฐานทดสอบ (ต้องขึ้นต้น Lockte และลงท้าย Uat/Test/Dev)" }
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
Write-Host "1/3 ตรวจธงเปิดใช้งานจริงและลบฐาน $Database ถ้ามี"
$exists = Invoke-Sql "$server;Database=master" "SELECT COUNT(*) FROM sys.databases WHERE name = N'$Database'"
if ($exists -gt 0) {
    $live = Invoke-Sql $connection "SELECT COUNT(*) FROM sys.extended_properties WHERE class = 0 AND name = N'Lockte.GoLive'"
    if ($live -gt 0) { throw "ฐาน $Database ตั้งธงเปิดใช้งานจริงแล้ว (Lockte.GoLive) — ไม่รีเซ็ต" }
    Invoke-Sql "$server;Database=master" "ALTER DATABASE [$Database] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$Database]" | Out-Null
}

Write-Host '2/3 สร้างโครงฐานข้อมูล (เว็บแอป migrate)'
$env:ConnectionStrings__Lockte = $connection
$env:Reports__WarmUpOnStart = 'false'
dotnet run --project (Join-Path $root 'src\Lockte.Web') -c Release -- reset-admin --password $AdminPassword | Out-Host
if ($LASTEXITCODE -ne 0) { throw 'สร้างโครงฐานข้อมูลไม่สำเร็จ' }

Write-Host '3/3 เติมข้อมูลปลอมของวัตถุดิบ'
dotnet run --project (Join-Path $root 'src\Lockte.Web') -c Release -- seed-purch-fake | Out-Host
if ($LASTEXITCODE -ne 0) { throw 'เติมข้อมูลปลอมไม่สำเร็จ' }

Write-Host ("เสร็จใน {0:0.0} นาที" -f $watch.Elapsed.TotalMinutes)
