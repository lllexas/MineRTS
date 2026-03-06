# Switch-Persona Script
param([string]$Name, [switch]$List, [switch]$Current)

$PersonaDir = "C:\Users\SeBenux\.qwen\personas"
$OutputFile = "C:\Users\SeBenux\.qwen\output-language.md"

if ($List) {
    Write-Host "Available personas:" -ForegroundColor Green
    Get-ChildItem $PersonaDir -Filter *.md | ForEach-Object {
        $name = $_.BaseName
        Write-Host "  - $name"
    }
    return
}

if ($Current) {
    Write-Host "Current persona config: $OutputFile"
    return
}

if ($Name -and $Name -ne "") {
    $personaFile = Join-Path $PersonaDir "$Name.md"
    if (Test-Path $personaFile) {
        Copy-Item $personaFile $OutputFile -Force
        Write-Host "Switched to: $Name" -ForegroundColor Green
    } else {
        Write-Host "Error: Persona '$Name' not found" -ForegroundColor Red
        Write-Host "Use -List to see available personas"
        exit 1
    }
} else {
    Write-Host "Usage:"
    Write-Host "  .\switch-persona.ps1 -Name <persona-name>"
    Write-Host "  .\switch-persona.ps1 -List"
    Write-Host "  .\switch-persona.ps1 -Current"
}
