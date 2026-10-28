<#
.SYNOPSIS
    Publishes the .NET broker and client apps, compiles the Inno Setup installer 
    with a dynamic endpoint.
.EXAMPLE
    .\Publish-App.ps1 1.2.3
    .\Publish-App.ps1 1.2.3 --remote
#>
[CmdletBinding()]
param(
    [Parameter(Position=0, Mandatory=$true)]
    [string]$Version,
    [switch]$Remote
)

Push-Location "$PSScriptRoot/.."

if ($Remote) {
    Write-Host "[INFO] --remote flag detected. Using remote endpoint." -ForegroundColor Cyan
    $TargetEndpoint = $env:SMAS_REMOTE_ENDPOINT
    $TargetPort = 8883
    if (-not $TargetEndpoint) {
        throw "Environment variable 'SMAS_REMOTE_ENDPOINT' is not set. Please set it to the remote endpoint URL."
    }
    $DeployBroker = "false"
} else {
    Write-Host "[INFO] Using localhost as app endpoint." -ForegroundColor Green
    $TargetEndpoint = "localhost"
    $TargetPort = 1883
    $DeployBroker = "true"
}

Write-Host "[INFO] Publishing .NET applications (Release configuration)..." -ForegroundColor Yellow

# Publish Broker Service (Adjust paths to match your project structure)
if ($DeployBroker -eq "true") {
    dotnet publish .\SmasKunovice.BrokerService\ -c Release -r win-x64 --self-contained -o ./publish/broker -p:Version=$Version
    if ($LASTEXITCODE -ne 0) { throw "Broker service publish failed." }
}

# Publish Desktop Client App
dotnet publish .\SmasKunovice.Avalonia\ -c Release -r win-x64 --self-contained -o ./publish/app -p:Version=$Version
if ($LASTEXITCODE -ne 0) { throw "Client app publish failed." }

$configPath = "publish\app\appsettings.json"
if (Test-Path $configPath) {
    Write-Host "[INFO] Updating Host in $configPath..." -ForegroundColor Yellow
    
    # Read and parse JSON
    $configJson = Get-Content -Raw -Path $configPath | ConvertFrom-Json
    
    # Ensure ClientAdapterOptions object and Host property exist, then update value
    if (-not $configJson.ClientAdapterOptions) {
        throw "ClientAdapterOptions section not found in appsettings.json."
    } else {
        $configJson.ClientAdapterOptions | Add-Member -Force -MemberType NoteProperty -Name "Host" -Value $TargetEndpoint
    }
    if (-not $configJson.ClientAdapterOptions) {
        throw "ClientAdapterOptions section not found in appsettings.json."
    } else {
        $configJson.ClientAdapterOptions | Add-Member -Force -MemberType NoteProperty -Name "Port" -Value $TargetPort
    }
    
    # Save back formatted JSON to the publish folder
    $configJson | ConvertTo-Json -Depth 10 | Set-Content -Path $configPath
} else {
    Write-Warning "appsettings.json not found in publish directory. Skipping direct endpoint injection."
}

# --- 2. COMPILE INNO SETUP INSTALLER ---
Write-Host "[INFO] Compiling Inno Setup installer..." -ForegroundColor Yellow
$isccPath = "${env:ProgramFiles(x86)}\Inno Setup 7\ISCC.exe"
if (-not (Test-Path $isccPath)) {
    $isccPath = "${env:ProgramFiles}\Inno Setup 7\ISCC.exe"
}
if (-not (Test-Path $isccPath)) {
    throw "Inno Setup Compiler (ISCC.exe) not found. Please verify Inno Setup is installed."
}

& $isccPath "setup.iss" "/DAppVersion=$Version"  "/DDeployBroker=$DeployBroker"
if ($LASTEXITCODE -ne 0) { throw "Installer compilation failed." }

Write-Host "[SUCCESS] Installer compiled successfully!" -ForegroundColor Green
Pop-Location