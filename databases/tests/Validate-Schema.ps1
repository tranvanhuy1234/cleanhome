param([string]$MariaDbBin = 'C:\xampp\mysql\bin')

$ErrorActionPreference = 'Stop'
$schemaRoot = Split-Path $PSScriptRoot -Parent
$installDb = Join-Path $MariaDbBin 'mysql_install_db.exe'
$server = Join-Path $MariaDbBin 'mysqld.exe'
if (!(Test-Path -LiteralPath $installDb) -or !(Test-Path -LiteralPath $server)) {
    throw 'Khong tim thay MariaDB binaries. Truyen -MariaDbBin dung thu muc XAMPP.'
}

# Chỉ tạo dữ liệu kiểm thử trong TEMP, không kết nối MariaDB đang chạy.
$testData = Join-Path ([IO.Path]::GetTempPath()) ('cleanhome-schema-' + [guid]::NewGuid().ToString('N'))
& $installDb "--datadir=$testData" --skip-networking --silent
if ($LASTEXITCODE -ne 0) { throw 'Khong khoi tao duoc database kiem thu.' }

function Invoke-Bootstrap([string]$Sql, [bool]$ShouldSucceed = $true, [int]$ExpectedError = 0) {
    $info = New-Object Diagnostics.ProcessStartInfo
    $info.FileName = $server
    $baseDir = Split-Path $MariaDbBin -Parent
    $info.Arguments = '--no-defaults --basedir="' + $baseDir + '" --datadir="' + $testData + '" --bootstrap --skip-networking --console --innodb-buffer-pool-size=32M'
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $info.RedirectStandardInput = $true
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    $process = [Diagnostics.Process]::Start($info)
    $stdout = $process.StandardOutput.ReadToEndAsync()
    $stderr = $process.StandardError.ReadToEndAsync()
    # Bootstrap yêu cầu mỗi statement kết thúc ở một dòng riêng.
    $process.StandardInput.WriteLine(($Sql -replace '(?m)^--[^\r\n]*', '' -replace '; ', ";`n"))
    $process.StandardInput.Close()
    if (!$process.WaitForExit(30000)) { $process.Kill(); throw 'MariaDB bootstrap timeout.' }
    $diagnostics = $stdout.Result + $stderr.Result
    if ($ShouldSucceed -and $process.ExitCode -ne 0) { throw $diagnostics }
    if (!$ShouldSucceed -and $process.ExitCode -eq 0) { throw 'Du lieu khong hop le da duoc chap nhan.' }
    if (!$ShouldSucceed -and $ExpectedError -gt 0 -and $diagnostics -notmatch ('ERROR:\s*' + $ExpectedError + '\b')) {
        throw ('Khong nhan dung ma loi MariaDB ' + $ExpectedError + ': ' + $diagnostics)
    }
    $process.Dispose()
}

$schemas = @(Get-ChildItem -Path (Join-Path $schemaRoot '*-db/001_initial.sql') -File | Sort-Object FullName)
if ($schemas.Count -ne 7) { throw 'Can du 7 schema service.' }
$combined = ($schemas | ForEach-Object { [IO.File]::ReadAllText($_.FullName) }) -join "`n"
Invoke-Bootstrap $combined
Write-Output 'PASS: imported all 7 schemas into isolated MariaDB.'

# Một đơn có hai nhân viên được đánh giá riêng; ID bên ngoài không cần FK xuyên DB.
Invoke-Bootstrap @'
USE cleanhome_review;
INSERT INTO reviews (booking_id, employee_id, customer_id, assignment_id, rating, comment, created_at)
VALUES (1, 11, 21, 31, 5, 'Good', UTC_TIMESTAMP()), (1, 12, 21, 32, 4, 'Good', UTC_TIMESTAMP());
'@
Write-Output 'PASS: separate reviews for two employees in one booking.'
Invoke-Bootstrap "USE cleanhome_review; INSERT INTO reviews (booking_id,employee_id,customer_id,assignment_id,rating,comment,created_at) VALUES (1,11,21,31,5,'Duplicate',UTC_TIMESTAMP());" $false 1062
Write-Output 'PASS: duplicate booking/employee review rejected.'
Invoke-Bootstrap "USE cleanhome_review; INSERT INTO reviews (booking_id,employee_id,customer_id,assignment_id,rating,comment,created_at) VALUES (2,11,21,33,6,'Invalid',UTC_TIMESTAMP());" $false 4025
Write-Output 'PASS: rating outside 1..5 rejected.'
Invoke-Bootstrap "USE cleanhome_auth; INSERT INTO accounts (email,password_hash) VALUES ('qa@example.invalid','test-only');"
Invoke-Bootstrap "USE cleanhome_auth; INSERT INTO accounts (email,password_hash) VALUES ('QA@example.invalid','test-only');" $false 1062
Write-Output 'PASS: duplicate email rejected regardless of case.'
Invoke-Bootstrap "USE cleanhome_payment; INSERT INTO payments (booking_id,subscription_id,amount,method,status,merchant_ref,idempotency_key,customer_id) VALUES (1,2,100,'vnpay','pending','qa-1','qa-1',21);" $false 4025
Write-Output 'PASS: payment cannot target both a booking and a subscription.'

Invoke-Bootstrap @'
USE cleanhome_order;
INSERT INTO bookings (id,customer_id,branch_id,address_snapshot,scheduled_start,scheduled_end,status,total_amount,code,subtotal_amount)
VALUES (1,21,1,'{}','2027-01-01 01:00:00','2027-01-01 03:00:00','pending',100,'QA-1',100);
INSERT INTO cancellation_requests (booking_id,requested_by,reason) VALUES (1,21,'Test cancel');
'@
Invoke-Bootstrap "USE cleanhome_order; INSERT INTO cancellation_requests (booking_id,requested_by,reason) VALUES (1,21,'Duplicate pending');" $false 1062
Invoke-Bootstrap @'
USE cleanhome_order;
UPDATE cancellation_requests SET status='rejected',resolved_by=99,resolved_at=UTC_TIMESTAMP() WHERE booking_id=1;
INSERT INTO cancellation_requests (booking_id,requested_by,reason) VALUES (1,21,'New request');
'@
Write-Output 'PASS: one pending cancellation per booking; processed history is retained.'

Invoke-Bootstrap @'
USE cleanhome_staff;
INSERT INTO branches (id,name,address_text,service_area,latitude,longitude,status) VALUES (1,'QA','QA','{}',0,0,'active');
INSERT INTO employees (id,user_id,branch_id,verification_status,experience_years,response_rate,status)
VALUES (11,41,1,'verified',0,100,'active'), (12,42,1,'verified',0,100,'active');
INSERT INTO assignments (booking_id,employee_id,assigned_at,status,scheduled_start,scheduled_end,assigned_by)
VALUES (1,11,UTC_TIMESTAMP(),'waiting','2027-01-01 01:00:00','2027-01-01 03:00:00',99),
(1,12,UTC_TIMESTAMP(),'waiting','2027-01-01 01:00:00','2027-01-01 03:00:00',99);
'@
Invoke-Bootstrap "USE cleanhome_staff; INSERT INTO assignments (booking_id,employee_id,assigned_at,status,scheduled_start,scheduled_end,assigned_by) VALUES (1,11,UTC_TIMESTAMP(),'waiting','2027-01-01 01:00:00','2027-01-01 03:00:00',99);" $false 1062
Write-Output 'PASS: one booking accepts multiple employees and rejects duplicate assignments.'
Invoke-Bootstrap "USE cleanhome_service; INSERT INTO vouchers (code,name,discount_type,discount_value,starts_at,expires_at) VALUES ('INVALID','QA','PERCENT',101,'2027-01-01','2027-02-01');" $false 4025
Invoke-Bootstrap "USE cleanhome_service; INSERT INTO service_packages (name,price,validity_days,is_active) VALUES ('Invalid',100,0,1);" $false 4025
Write-Output 'PASS: invalid voucher percentage and package validity rejected.'
Write-Output "Temporary test data retained at: $testData"
