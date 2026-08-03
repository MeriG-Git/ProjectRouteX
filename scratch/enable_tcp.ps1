# SQL Server レジストリパスの自動検出・TCP 1433有効化スクリプト
Get-ChildItem "HKLM:\SOFTWARE\Microsoft\Microsoft SQL Server" | Where-Object { $_.PSChildName -like "MSSQL*" } | ForEach-Object {
    $tcp = $_.PSPath + "\MSSQLServer\SuperSocketNetLib\Tcp"
    if (Test-Path $tcp) {
        Set-ItemProperty -Path $tcp -Name "Enabled" -Value 1 -ErrorAction SilentlyContinue
        Write-Host "TCP Enabled set to 1 on $tcp"
        $ipAll = $tcp + "\IPAll"
        if (Test-Path $ipAll) {
            Set-ItemProperty -Path $ipAll -Name "TcpPort" -Value "1433" -ErrorAction SilentlyContinue
            Set-ItemProperty -Path $ipAll -Name "TcpDynamicPorts" -Value "" -ErrorAction SilentlyContinue
            Write-Host "TcpPort set to 1433 on $ipAll"
        }
    }
}

try {
    Restart-Service MSSQLSERVER -ErrorAction Stop
    Write-Host "MSSQLSERVER service restarted successfully."
} catch {
    Write-Host "Service restart: $_"
}
