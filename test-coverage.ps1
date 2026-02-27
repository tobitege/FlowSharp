param(
  [ValidateSet("Debug", "Release")]
  [string]$Configuration = "Debug",
  [ValidateRange(1, 100)]
  [int]$LineThreshold = 60
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $repoRoot

$coverageDir = Join-Path $repoRoot "artifacts\coverage\flowsharp-main"
New-Item -ItemType Directory -Path $coverageDir -Force | Out-Null

$outputPrefix = Join-Path $coverageDir "coverage"
if (Test-Path "$outputPrefix*") {
  Remove-Item "$outputPrefix*" -Force
}

$excludeByFile = "**/Program.cs%2c**/*.Designer.cs"

Write-Host "Running FlowSharp main coverage gate (line threshold: $LineThreshold%)"

dotnet test "Tests/FlowSharp.Main.Tests/FlowSharp.Main.Tests.csproj" `
  -c $Configuration `
  -v minimal `
  /p:CollectCoverage=true `
  /p:Include="[FlowSharp]*" `
  /p:CoverletOutput="$outputPrefix" `
  /p:CoverletOutputFormat=cobertura `
  /p:ExcludeByFile="$excludeByFile" `
  /p:Threshold=$LineThreshold `
  /p:ThresholdType=line `
  /p:ThresholdStat=total

if ($LASTEXITCODE -ne 0) {
  throw "Coverage gate failed with exit code $LASTEXITCODE."
}

Write-Host "Coverage report files:"
Get-ChildItem $coverageDir | ForEach-Object { Write-Host " - $($_.FullName)" }
