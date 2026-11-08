$ErrorActionPreference='Stop'
$taskBackend=Split-Path $PSScriptRoot -Parent
$taskTunnelRoot=Join-Path $taskBackend '.local/tunnel'
$taskBinary=Join-Path $taskTunnelRoot 'cloudflared.exe'
if(!(Test-Path -LiteralPath $taskBinary)){throw 'Download the official cloudflared Windows executable to backend/.local/tunnel/cloudflared.exe first.'}
$taskProxy=Start-Process node -ArgumentList @((Join-Path $PSScriptRoot 'vnpay-callback-proxy.cjs')) -WindowStyle Hidden -WorkingDirectory $taskBackend -RedirectStandardOutput (Join-Path $taskTunnelRoot 'vnpay-proxy.out.log') -RedirectStandardError (Join-Path $taskTunnelRoot 'vnpay-proxy.err.log') -PassThru
$taskProxy.Id | Set-Content (Join-Path $taskTunnelRoot 'vnpay-proxy.pid')
$taskTunnel=Start-Process $taskBinary -ArgumentList @('tunnel','--no-autoupdate','--url','http://127.0.0.1:4090','--protocol','http2') -WindowStyle Hidden -WorkingDirectory $taskTunnelRoot -RedirectStandardOutput (Join-Path $taskTunnelRoot 'vnpay.out.log') -RedirectStandardError (Join-Path $taskTunnelRoot 'vnpay.err.log') -PassThru
$taskTunnel.Id | Set-Content (Join-Path $taskTunnelRoot 'vnpay.pid')
Write-Output 'Tunnel started. URL will appear in backend/.local/tunnel/vnpay.err.log. Keep both processes running.'
