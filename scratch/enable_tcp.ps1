$ipAll = "HKLM:\SOFTWARE\Microsoft\Microsoft SQL Server\MSSQL16.MSSQLSERVER\MSSQLServer\SuperSocketNetLib\Tcp\IPAll"
if (Test-Path $ipAll) {
    Set-ItemProperty -Path $ipAll -Name "TcpPort" -Value "1433" -ErrorAction SilentlyContinue
    Set-ItemProperty -Path $ipAll -Name "TcpDynamicPorts" -Value "" -ErrorAction SilentlyContinue
    Write-Host "IPAll TcpPort set to 1433"
}

$tcpPath = "HKLM:\SOFTWARE\Microsoft\Microsoft SQL Server\MSSQL16.MSSQLSERVER\MSSQLServer\SuperSocketNetLib\Tcp"
if (Test-Path $tcpPath) {
    Set-ItemProperty -Path $tcpPath -Name "Enabled" -Value 1 -ErrorAction SilentlyContinue
    Write-Host "TCP Enabled set to 1"
}

try {
    Restart-Service MSSQLSERVER -ErrorAction Stop
    Write-Host "MSSQLSERVER service restarted successfully."
} catch {
    Write-Host "Service restart message: $_"
}
