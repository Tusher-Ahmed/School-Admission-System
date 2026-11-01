<#
.SYNOPSIS
  Bonus check: two backend processes (ports 8080 and 8081) share one database and
  together must never hold more seats than a class has.

.EXAMPLE
  cd backend
  dotnet build
  powershell -ExecutionPolicy Bypass -File scripts/two-process-check.ps1 -Seats 10 -Requests 60
#>
param(
    [int]$Seats = 10,
    [int]$Requests = 60
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Net.Http

$project = Join-Path $PSScriptRoot "..\src\SchoolAdmission.Api"
$ports = 8080, 8081
$procs = @()

$http = [System.Net.Http.HttpClient]::new()
$http.Timeout = [TimeSpan]::FromSeconds(30)

function Get-Json([string]$url) { $http.GetStringAsync($url).Result | ConvertFrom-Json }

function Wait-Healthy([string]$url) {
    for ($i = 0; $i -lt 60; $i++) {
        try { Get-Json "$url/health" | Out-Null; return } catch { Start-Sleep -Milliseconds 500 }
    }
    throw "API at $url did not start"
}

try {
    foreach ($port in $ports) {
        Write-Host "Starting backend on port $port ..."
        $log = Join-Path $env:TEMP "school-admission-$port.log"
        $procs += Start-Process dotnet -ArgumentList "run", "--project", "`"$project`"", "--no-build", "--urls", "http://localhost:$port" `
            -PassThru -WindowStyle Hidden -RedirectStandardOutput $log -RedirectStandardError "$log.err"
    }
    foreach ($port in $ports) { Wait-Healthy "http://localhost:$port" }

    $body = [System.Net.Http.StringContent]::new(
        "{""name"":""TEST two-process $(Get-Date -Format s)"",""totalSeats"":$Seats}", [Text.Encoding]::UTF8, "application/json")
    $created = $http.PostAsync("http://localhost:8080/classes", $body).Result.Content.ReadAsStringAsync().Result | ConvertFrom-Json
    Write-Host "Class $($created.id) with $Seats seats. Sending $Requests applications at once, split across both processes ..."

    $rand = [Random]::new()
    $tasks = for ($i = 0; $i -lt $Requests; $i++) {
        $port = $ports[$i % 2]
        $student = "9" + (-join (1..16 | ForEach-Object { $rand.Next(10) }))
        $req = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::Post, "http://localhost:$port/classes/$($created.id)/holds")
        $req.Headers.Add("X-Student-Id", $student)
        $http.SendAsync($req)
    }
    [System.Threading.Tasks.Task]::WaitAll([System.Threading.Tasks.Task[]]$tasks)

    $codes = $tasks | ForEach-Object { [int]$_.Result.StatusCode } | Group-Object | ForEach-Object { "$($_.Name) x $($_.Count)" }
    Write-Host "Responses: $($codes -join ', ')"

    $a = Get-Json "http://localhost:8080/classes/$($created.id)"
    $b = Get-Json "http://localhost:8081/classes/$($created.id)"
    Write-Host "Seen from 8080: held=$($a.heldSeats) confirmed=$($a.confirmedSeats) available=$($a.availableSeats)"
    Write-Host "Seen from 8081: held=$($b.heldSeats) confirmed=$($b.confirmedSeats) available=$($b.availableSeats)"

    $created201 = @($tasks | Where-Object { [int]$_.Result.StatusCode -eq 201 }).Count
    if ($created201 -eq $Seats -and $a.heldSeats -eq $Seats -and $b.heldSeats -eq $Seats) {
        Write-Host "PASS: exactly $Seats holds across two processes." -ForegroundColor Green
    } else {
        Write-Host "FAIL: $created201 holds created for $Seats seats." -ForegroundColor Red
        exit 1
    }
}
finally {
    Get-CimInstance Win32_Process -Filter "Name='SchoolAdmission.Api.exe'" | ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
    $procs | ForEach-Object { Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue }
}
