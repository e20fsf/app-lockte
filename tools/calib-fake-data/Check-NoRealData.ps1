<#
.SYNOPSIS
  ตรวจว่าไฟล์ที่ track ใน git ไม่มีชื่อผู้สอบเทียบหรือบริษัทที่สอบเทียบจริง (สอบเทียบ #210, ข้อตัดสิน Q-25).

.DESCRIPTION
  อ่านชื่อที่ไม่ซ้ำจากสำเนา สอบเทียบ.accdb (ผู้สอบเทียบทุกชนิด + บริษัทที่สอบเทียบ) แล้วค้นด้วย git grep ในไฟล์ที่ track ทั้งหมด — เอกสาร เทสต์ fixture คอมเมนต์ ผลรายงาน.
  แสดงเฉพาะ **จำนวน** ที่พบและชื่อไฟล์ ไม่แสดงชื่อ; exit code 1 ถ้าพบ. ไม่แก้ไฟล์ต้นทาง (อ่านจากสำเนาในโฟลเดอร์ temp). ต้องมี Access Database Engine (ACE) บน Windows.

.EXAMPLE
  .\tools\calib-fake-data\Check-NoRealData.ps1 -Source .\source\สอบเทียบ.accdb
#>
[CmdletBinding()]
param([Parameter(Mandatory)] [string] $Source)
$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..\..')
if (-not (Test-Path $Source)) { throw "ไม่พบไฟล์ $Source" }

$copy = Join-Path ([IO.Path]::GetTempPath()) ("calib-check-" + [guid]::NewGuid().ToString('N') + '.accdb')
Copy-Item -LiteralPath $Source -Destination $copy
try {
    $connection = New-Object System.Data.OleDb.OleDbConnection "Provider=Microsoft.ACE.OLEDB.16.0;Data Source=$copy;Mode=Read"
    $connection.Open()
    $names = New-Object 'System.Collections.Generic.HashSet[string]'
    foreach ($pair in @(@('CALIPER', 'ผู้สอบเทียบ'), @('PRESSURE GAUGE', 'ผู้สอบเทียบ'), @('เครื่องชั่ง', 'ผู้สอบเทียบ'), @('ตัวควบคุมอุณหภูมิ', 'ผู้สอบเทียบ'), @('jig', 'ผู้สอบเทียบ'),
                        @('ภายนอก', 'ผู้สอบเทียบ'), @('บริษัทที่สอบเทียบ', 'NAME'))) {
        $command = $connection.CreateCommand()
        $command.CommandText = "SELECT DISTINCT [$($pair[1])] FROM [$($pair[0])] WHERE [$($pair[1])] IS NOT NULL"
        $reader = $command.ExecuteReader()
        while ($reader.Read()) { $value = ([string]$reader[0]).Trim(); if ($value.Length -ge 4) { [void]$names.Add($value) } }
        $reader.Close()
    }
    $connection.Close()
} finally { Remove-Item -LiteralPath $copy -Force -ErrorAction SilentlyContinue }

Push-Location $root
try {
    $hits = 0
    foreach ($name in $names) {
        $files = @(git grep -l -i -F -- $name 2>$null)
        if ($files.Count -gt 0) { $hits++; Write-Warning ("พบชื่อจริง 1 ชื่อ (ยาว {0} ตัวอักษร) ในไฟล์: {1}" -f $name.Length, ($files -join ', ')) }
    }
    Write-Host ("ตรวจ {0} ชื่อ — พบในไฟล์ที่ track {1} ชื่อ" -f $names.Count, $hits)
    if ($hits -gt 0) { exit 1 }
} finally { Pop-Location }
