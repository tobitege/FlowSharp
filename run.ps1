param(
  [ValidateSet("Debug", "Release")]
  [string]$Configuration = "Debug",
  [switch]$NoBuild
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $repoRoot

function Resolve-MSBuildPath {
  $vswhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"

  if (!(Test-Path $vswhere)) {
    throw "vswhere.exe was not found. Install Visual Studio with MSBuild tools."
  }

  $msbuild = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\MSBuild.exe" | Select-Object -First 1

  if ([string]::IsNullOrWhiteSpace($msbuild)) {
    throw "MSBuild.exe was not found by vswhere."
  }

  return $msbuild
}

if (-not $NoBuild) {
  $msbuildPath = Resolve-MSBuildPath
  Write-Host "Building FlowSharp ($Configuration) using $msbuildPath"
  & $msbuildPath "FlowSharp.csproj" /t:Build /p:Configuration=$Configuration /p:Platform=AnyCPU /m:1 /v:m

  if ($LASTEXITCODE -ne 0) {
    throw "Build failed with exit code $LASTEXITCODE."
  }
}

$exePath = Join-Path $repoRoot "bin\$Configuration\FlowSharp.exe"

if (!(Test-Path $exePath)) {
  throw "Executable not found at '$exePath'. Run without -NoBuild first."
}

Write-Host "Starting $exePath"
$workingDir = Split-Path $exePath
$moduleFile = Join-Path $workingDir "modules.xml"
$arguments = @()

if (Test-Path $moduleFile) {
  $arguments += $moduleFile
}

Start-Process -FilePath $exePath -WorkingDirectory $workingDir -ArgumentList $arguments
