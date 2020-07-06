#!/usr/bin/env pwsh

<#
This script runs locally the checks from the continous integration.
#>

function Main
{
    Push-Location

    Set-Location $PSScriptRoot

    Write-Host "Checking the format..."
    dotnet format --check
    if ($LASTEXITCODE -ne 0)
    {
        throw "Format check failed."
    }

    Write-Host "Checking the line length and number of lines per file..."
    dotnet bite-sized --inputs '**/*.cs' --excludes '**/obj/**'
    if ($LASTEXITCODE -ne 0)
    {
        throw "The check of line width failed."
    }

    Write-Host "Checking the dead code..."
    dotnet dead-csharp --inputs '**/*.cs' --excludes '**/obj/**'
    if ($LASTEXITCODE -ne 0)
    {
        throw "The check of dead code failed."
    }

    Write-Host "Running the unit tests..."
    dotnet test /p:CollectCoverage=true
    if ($LASTEXITCODE -ne 0)
    {
        throw "The unit tests failed."
    }

    Pop-Location
}

Main