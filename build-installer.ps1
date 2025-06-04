# Build script for Microphone Volume Enforcer installer
# Requires Inno Setup to be installed: https://jrsoftware.org/isinfo.php

param(
    [string]$Configuration = "Release",
    [switch]$SkipBuild = $false
)

Write-Host "Building Microphone Volume Enforcer Installer..." -ForegroundColor Green

# Check if Inno Setup is installed
$InnoSetupPath = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
if (-not (Test-Path $InnoSetupPath)) {
    $InnoSetupPath = "${env:ProgramFiles}\Inno Setup 6\ISCC.exe"
    if (-not (Test-Path $InnoSetupPath)) {
        Write-Error "Inno Setup not found. Please install from: https://jrsoftware.org/isinfo.php"
        exit 1
    }
}

# Build the application if not skipped
if (-not $SkipBuild) {
    Write-Host "Building application..." -ForegroundColor Yellow
    
    # Clean previous builds
    if (Test-Path "bin") { Remove-Item "bin" -Recurse -Force }
    if (Test-Path "obj") { Remove-Item "obj" -Recurse -Force }
    
    # Build and publish
    dotnet publish -c $Configuration -r win-x64 --self-contained false -p:PublishSingleFile=false -o "bin\$Configuration\net8.0-windows\publish"
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed!"
        exit 1
    }
    
    Write-Host "Build completed successfully!" -ForegroundColor Green
}

# Create installer directory
if (-not (Test-Path "installer")) {
    New-Item -ItemType Directory -Path "installer" | Out-Null
}

# Compile installer
Write-Host "Creating installer..." -ForegroundColor Yellow
& $InnoSetupPath "MicrophoneVolumeEnforcer.iss"

if ($LASTEXITCODE -ne 0) {
    Write-Error "Installer creation failed!"
    exit 1
}

Write-Host "Installer created successfully!" -ForegroundColor Green
Write-Host "Output: installer\MicrophoneVolumeEnforcer-Setup.exe" -ForegroundColor Cyan

# Show file size
$InstallerPath = "installer\MicrophoneVolumeEnforcer-Setup.exe"
if (Test-Path $InstallerPath) {
    $FileSize = [math]::Round((Get-Item $InstallerPath).Length / 1MB, 2)
    Write-Host "Installer size: $FileSize MB" -ForegroundColor Cyan
} 