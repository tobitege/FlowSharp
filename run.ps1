param(
  [ValidateSet("Debug", "Release")]
  [string]$Configuration = "Debug",
  [switch]$NoBuild,
  [switch]$VerboseBuild
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
  $logDir = Join-Path $repoRoot "artifacts\logs"
  New-Item -Path $logDir -ItemType Directory -Force | Out-Null
  $buildLogPath = Join-Path $logDir ("run-build-{0}.log" -f $Configuration.ToLowerInvariant())

  Write-Host "Building FlowSharp ($Configuration) using $msbuildPath"
  Write-Host "Build log: $buildLogPath"

  $msbuildArgs = @(
    "FlowSharp.csproj"
    "/t:Build"
    "/p:Configuration=$Configuration"
    "/p:Platform=AnyCPU"
    "/m:1"
    "/nologo"
    "/fl"
    "/flp:logfile=$buildLogPath;verbosity=normal"
  )

  if ($VerboseBuild) {
    $msbuildArgs += "/v:m"
  }
  else {
    $msbuildArgs += "/v:minimal"
    $msbuildArgs += "/clp:ErrorsOnly;Summary"
  }

  & $msbuildPath @msbuildArgs

  if ($LASTEXITCODE -ne 0) {
    throw "Build failed with exit code $LASTEXITCODE."
  }
}

$candidateExePaths = @(
  (Join-Path $repoRoot "bin\$Configuration\net8.0-windows\FlowSharp.exe"),
  (Join-Path $repoRoot "bin\$Configuration\FlowSharp.exe")
)

$exePath = $candidateExePaths | Where-Object { Test-Path $_ } | Select-Object -First 1

if ([string]::IsNullOrWhiteSpace($exePath)) {
  $searchedPaths = $candidateExePaths -join "', '"
  throw "Executable not found. Checked: '$searchedPaths'. Run without -NoBuild first."
}

Write-Host "Starting $exePath"
$workingDir = Split-Path $exePath
$moduleFile = Join-Path $workingDir "modules.xml"
$arguments = @()

if (Test-Path $moduleFile) {
  $arguments += $moduleFile
}

Start-Process -FilePath $exePath -WorkingDirectory $workingDir -ArgumentList $arguments
