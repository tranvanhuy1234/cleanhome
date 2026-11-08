param([string]$MariaDbBin = 'C:\xampp\mysql\bin', [switch]$ResetEmptyCleanHomeDatabases)

$ErrorActionPreference = 'Stop'
if (!$ResetEmptyCleanHomeDatabases) { throw 'Can truyen -ResetEmptyCleanHomeDatabases de xac nhan reset dung 7 database CleanHome.' }
$client = Join-Path $MariaDbBin 'mysql.exe'
$names = @('auth','user','service','order','staff','payment','review')
# Danh sách tên cố định; không nhận tên database từ đầu vào bên ngoài.
$statements = @('SET FOREIGN_KEY_CHECKS=0;')
foreach ($name in $names) { $statements += ('DROP DATABASE IF EXISTS `cleanhome_' + $name + '`;') }
$statements += 'SET FOREIGN_KEY_CHECKS=1;'
foreach ($name in $names) { $statements += [IO.File]::ReadAllText((Join-Path $PSScriptRoot ($name + '-db/001_initial.sql'))) }
# XAMPP local hiện tại dùng root không mật khẩu. Chỉ reset khi người dùng đã cho phép.
# Nếu tài khoản thay đổi, dùng file cấu hình client riêng; không thêm mật khẩu vào source.
$sql = $statements -join "`n"
$sql | & $client --no-defaults --protocol=TCP --host=127.0.0.1 --port=3306 --user=root --default-character-set=utf8mb4
if ($LASTEXITCODE -ne 0) { throw 'Reset/import that bai. Dung lai va kiem tra schema; DDL khong rollback ca file.' }
Write-Output 'Updated exactly 7 cleanhome_* databases. Other databases were not changed.'
