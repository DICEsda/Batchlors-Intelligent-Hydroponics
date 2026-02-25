#Requires -Version 5.1
<#
.SYNOPSIS
    Provisions Azure Digital Twins for the Intelligent Hydroponics project.

.DESCRIPTION
    This script creates an Azure Digital Twins instance, assigns the required
    role, and uploads the DTDL models. Requires the Azure CLI (az) to be
    installed and logged in.

    Run: az login
    Then: .\scripts\setup-adt.ps1

.PARAMETER ResourceGroup
    Azure resource group name. Will be created if it doesn't exist.

.PARAMETER InstanceName
    Azure Digital Twins instance name (globally unique).

.PARAMETER Location
    Azure region. Defaults to westeurope.

.EXAMPLE
    .\scripts\setup-adt.ps1 -ResourceGroup "hydroponics-rg" -InstanceName "hydroponics-adt"
#>

param(
    [Parameter(Mandatory = $false)]
    [string]$ResourceGroup = "hydroponics-rg",

    [Parameter(Mandatory = $false)]
    [string]$InstanceName = "hydroponics-adt",

    [Parameter(Mandatory = $false)]
    [string]$Location = "westeurope"
)

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "=== Azure Digital Twins Setup ===" -ForegroundColor Cyan
Write-Host ""

# ---------------------------------------------------------------------------
# 0. Check prerequisites
# ---------------------------------------------------------------------------
Write-Host "[0/5] Checking prerequisites..." -ForegroundColor Yellow

try {
    $azVersion = az version 2>$null | ConvertFrom-Json
    Write-Host "  Azure CLI version: $($azVersion.'azure-cli')" -ForegroundColor Green
}
catch {
    Write-Host "  ERROR: Azure CLI (az) is not installed or not in PATH." -ForegroundColor Red
    Write-Host "  Install from: https://learn.microsoft.com/en-us/cli/azure/install-azure-cli-windows" -ForegroundColor Red
    exit 1
}

# Check login status
$account = az account show 2>$null | ConvertFrom-Json
if (-not $account) {
    Write-Host "  Not logged in. Running 'az login'..." -ForegroundColor Yellow
    az login
    $account = az account show | ConvertFrom-Json
}
Write-Host "  Logged in as: $($account.user.name)" -ForegroundColor Green
Write-Host "  Subscription: $($account.name) ($($account.id))" -ForegroundColor Green

# Check for Digital Twins extension
$extensions = az extension list 2>$null | ConvertFrom-Json
$dtExtension = $extensions | Where-Object { $_.name -eq "azure-iot" }
if (-not $dtExtension) {
    Write-Host "  Installing azure-iot extension..." -ForegroundColor Yellow
    az extension add --name azure-iot --yes
    Write-Host "  azure-iot extension installed." -ForegroundColor Green
}
else {
    Write-Host "  azure-iot extension: installed" -ForegroundColor Green
}

Write-Host ""

# ---------------------------------------------------------------------------
# 1. Create resource group (if needed)
# ---------------------------------------------------------------------------
Write-Host "[1/5] Creating resource group '$ResourceGroup' in '$Location'..." -ForegroundColor Yellow

$rgExists = az group exists --name $ResourceGroup 2>$null
if ($rgExists -eq "true") {
    Write-Host "  Resource group already exists." -ForegroundColor Green
}
else {
    az group create --name $ResourceGroup --location $Location --output none
    Write-Host "  Resource group created." -ForegroundColor Green
}

Write-Host ""

# ---------------------------------------------------------------------------
# 2. Create Azure Digital Twins instance
# ---------------------------------------------------------------------------
Write-Host "[2/5] Creating Azure Digital Twins instance '$InstanceName'..." -ForegroundColor Yellow

$existing = az dt show --dt-name $InstanceName --resource-group $ResourceGroup 2>$null | ConvertFrom-Json
if ($existing) {
    Write-Host "  Instance already exists." -ForegroundColor Green
    $hostName = $existing.hostName
}
else {
    Write-Host "  This may take 1-2 minutes..." -ForegroundColor Gray
    $result = az dt create `
        --dt-name $InstanceName `
        --resource-group $ResourceGroup `
        --location $Location `
        --output json 2>$null | ConvertFrom-Json

    $hostName = $result.hostName
    Write-Host "  Instance created." -ForegroundColor Green
}

$instanceUrl = "https://$hostName"
Write-Host "  Instance URL: $instanceUrl" -ForegroundColor Cyan

Write-Host ""

# ---------------------------------------------------------------------------
# 3. Assign 'Azure Digital Twins Data Owner' role
# ---------------------------------------------------------------------------
Write-Host "[3/5] Assigning 'Azure Digital Twins Data Owner' role..." -ForegroundColor Yellow

$userObjectId = $account.user.name
try {
    az dt role-assignment create `
        --dt-name $InstanceName `
        --resource-group $ResourceGroup `
        --assignee $userObjectId `
        --role "Azure Digital Twins Data Owner" `
        --output none 2>$null
    Write-Host "  Role assigned to $userObjectId" -ForegroundColor Green
}
catch {
    Write-Host "  Role may already be assigned (this is fine)." -ForegroundColor Yellow
}

Write-Host ""

# ---------------------------------------------------------------------------
# 4. Upload DTDL models (in dependency order)
# ---------------------------------------------------------------------------
Write-Host "[4/5] Uploading DTDL models..." -ForegroundColor Yellow

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
if (-not (Test-Path $repoRoot)) {
    $repoRoot = Split-Path -Parent $PSScriptRoot
}

$dtdlDir = Join-Path $repoRoot "infrastructure" "dtdl"
if (-not (Test-Path $dtdlDir)) {
    # Try relative to script location
    $dtdlDir = Join-Path (Split-Path -Parent $PSScriptRoot) "infrastructure" "dtdl"
}

# Upload order matters: models referenced by relationships must exist first
# Farm has no dependencies
# Reservoir has no dependencies  
# Tower has no dependencies
# Coordinator references Tower and Reservoir via relationships
$modelFiles = @(
    "Farm.json",
    "Reservoir.json",
    "Tower.json",
    "Coordinator.json"
)

foreach ($modelFile in $modelFiles) {
    $modelPath = Join-Path $dtdlDir $modelFile
    if (-not (Test-Path $modelPath)) {
        Write-Host "  WARNING: $modelFile not found at $modelPath" -ForegroundColor Red
        continue
    }

    $modelName = [System.IO.Path]::GetFileNameWithoutExtension($modelFile)
    Write-Host "  Uploading $modelName..." -NoNewline

    try {
        az dt model create `
            --dt-name $InstanceName `
            --resource-group $ResourceGroup `
            --models $modelPath `
            --output none 2>$null
        Write-Host " OK" -ForegroundColor Green
    }
    catch {
        # Model may already exist
        Write-Host " (already exists or updated)" -ForegroundColor Yellow
    }
}

Write-Host ""

# ---------------------------------------------------------------------------
# 5. Print configuration
# ---------------------------------------------------------------------------
Write-Host "[5/5] Setup complete!" -ForegroundColor Green
Write-Host ""
Write-Host "=== Configuration ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "Add the following to appsettings.Development.json:" -ForegroundColor Yellow
Write-Host ""
Write-Host @"
  "AzureDigitalTwins": {
    "InstanceUrl": "$instanceUrl",
    "TenantId": null,
    "ClientId": null,
    "ClientSecret": null,
    "TimeoutSeconds": 30,
    "EnableVerboseLogging": true
  }
"@ -ForegroundColor White
Write-Host ""
Write-Host "Authentication: Since you're logged in via 'az login'," -ForegroundColor Gray
Write-Host "DefaultAzureCredential will pick up your credentials automatically." -ForegroundColor Gray
Write-Host "No TenantId/ClientId/ClientSecret needed for local development." -ForegroundColor Gray
Write-Host ""
Write-Host "To verify, run:" -ForegroundColor Yellow
Write-Host "  az dt model list --dt-name $InstanceName -g $ResourceGroup --output table" -ForegroundColor White
Write-Host ""
Write-Host "Instance URL (copy this): $instanceUrl" -ForegroundColor Cyan
Write-Host ""
