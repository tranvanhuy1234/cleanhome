$ErrorActionPreference = 'Stop'
$backendRoot = Split-Path $PSScriptRoot -Parent
$processes = New-Object 'Collections.Generic.List[Diagnostics.Process]'

function Get-FreePort {
    $listener = New-Object Net.Sockets.TcpListener([Net.IPAddress]::Loopback, 0)
    $listener.Start()
    $port = $listener.LocalEndpoint.Port
    $listener.Stop()
    return $port
}

function Start-TestService([string]$Directory, [string]$Assembly, [int]$Port, [hashtable]$Overrides = @{}) {
    $directoryPath = Join-Path $backendRoot ($Directory + '/src')
    $dll = Join-Path $directoryPath ('bin/Debug/net10.0/' + $Assembly + '.dll')
    if (!(Test-Path -LiteralPath $dll)) { throw 'Chay dotnet build CleanHome.slnx truoc khi smoke test.' }
    $info = New-Object Diagnostics.ProcessStartInfo
    $info.FileName = 'dotnet'
    $info.Arguments = '"' + $dll + '"'
    $info.WorkingDirectory = $directoryPath
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $info.EnvironmentVariables['ASPNETCORE_URLS'] = "http://127.0.0.1:$Port"
    $info.EnvironmentVariables['ASPNETCORE_ENVIRONMENT'] = 'Production'
    # Không kết nối DB thật trong smoke test, kể cả khi đã có file cấu hình local.
    $info.EnvironmentVariables['ConnectionStrings__Database'] = ''
    foreach ($key in $Overrides.Keys) { $info.EnvironmentVariables[$key] = $Overrides[$key] }
    $process = [Diagnostics.Process]::Start($info)
    $processes.Add($process)
    $deadline = [DateTime]::UtcNow.AddSeconds(20)
    do {
        if ($process.HasExited) { throw "$Directory exited before becoming ready." }
        try {
            $response = Invoke-WebRequest "http://127.0.0.1:$Port/health/live" -UseBasicParsing -TimeoutSec 2
            if ($response.StatusCode -eq 200) { return }
        } catch { Start-Sleep -Milliseconds 100 }
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "$Directory startup timeout."
}

try {
    $authPort = Get-FreePort
    $gatewayPort = Get-FreePort
    while ($gatewayPort -eq $authPort) { $gatewayPort = Get-FreePort }
    Start-TestService 'auth-service' 'CleanHome.Auth.Api' $authPort
    Start-TestService 'api-gateway' 'CleanHome.Gateway.Api' $gatewayPort @{
        'ReverseProxy__Clusters__auth__Destinations__local__Address' = "http://127.0.0.1:$authPort/"
    }
    $identity = Invoke-RestMethod "http://127.0.0.1:$gatewayPort/api/auth/"
    if ($identity.service -ne 'auth-service') { throw 'Gateway did not route to auth-service.' }
    Write-Output 'PASS: gateway routes /api/auth/ to auth-service.'
    $live = Invoke-WebRequest "http://127.0.0.1:$gatewayPort/api/auth/health/live" -UseBasicParsing
    if ($live.StatusCode -ne 200) { throw 'Proxied liveness failed.' }
    Write-Output 'PASS: direct and proxied liveness return HTTP 200.'
    $readyStatus = 0
    try { $readyStatus = (Invoke-WebRequest "http://127.0.0.1:$authPort/health/ready" -UseBasicParsing).StatusCode }
    catch {
        if ($null -eq $_.Exception.Response) { throw }
        $readyStatus = [int]$_.Exception.Response.StatusCode
    }
    if ($readyStatus -ne 503) { throw 'Missing database must return HTTP 503 readiness.' }
    Write-Output 'PASS: missing database returns HTTP 503 readiness.'
    $allowed = Invoke-WebRequest "http://127.0.0.1:$gatewayPort/api/auth/health/live" -UseBasicParsing -Headers @{ Origin = 'http://localhost:3001' }
    if ($allowed.Headers['Access-Control-Allow-Origin'] -ne 'http://localhost:3001') { throw 'Expected customer CORS origin.' }
    $blocked = Invoke-WebRequest "http://127.0.0.1:$gatewayPort/api/auth/health/live" -UseBasicParsing -Headers @{ Origin = 'https://untrusted.example.invalid' }
    if ($blocked.Headers['Access-Control-Allow-Origin']) { throw 'Unexpected CORS access for untrusted origin.' }
    Write-Output 'PASS: CORS allows configured frontend origins only.'
} finally {
    foreach ($process in $processes) {
        if (!$process.HasExited) { $process.Kill(); $process.WaitForExit() }
        $process.Dispose()
    }
}
