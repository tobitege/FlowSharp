param(
    [string]$Uri,
    [int]$ConnectTimeoutSeconds = 3
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Assert-Equal {
    param(
        [object]$Expected,
        [object]$Actual,
        [string]$Message
    )

    if ($Expected -ne $Actual) {
        throw "$Message Expected=[$Expected] Actual=[$Actual]"
    }
}

function Get-CommandResponseJson {
    param(
        [object[]]$Results,
        [string]$CommandPrefix,
        [int]$Occurrence = 1
    )

    $matchingResults = @($Results | Where-Object { $_.Command.StartsWith($CommandPrefix, [System.StringComparison]::OrdinalIgnoreCase) })
    if ($matchingResults.Count -lt $Occurrence) {
        throw "Macro command was not found: $CommandPrefix occurrence $Occurrence."
    }

    $index = $Occurrence - 1
    $matchedResult = $matchingResults[$index]
    $responseText = [string]($matchedResult.Response)
    if ([string]::IsNullOrWhiteSpace($responseText)) {
        throw "Macro command did not return JSON: $CommandPrefix occurrence $Occurrence."
    }

    return $responseText | ConvertFrom-Json
}

function Resolve-FlowSharpUri {
    param([string]$Endpoint)

    if (-not [string]::IsNullOrWhiteSpace($Endpoint)) {
        return $Endpoint
    }

    if (-not [string]::IsNullOrWhiteSpace($env:FLOWSHARP_REPL_URI)) {
        return $env:FLOWSHARP_REPL_URI
    }

    if (-not [string]::IsNullOrWhiteSpace($env:FLOWSHARP_WEBSOCKET_PORT)) {
        return "ws://localhost:$($env:FLOWSHARP_WEBSOCKET_PORT)/flowsharp/"
    }

    return "ws://localhost:1100/flowsharp/"
}

function Test-HasExplicitEndpoint {
    return -not [string]::IsNullOrWhiteSpace($Uri) `
        -or -not [string]::IsNullOrWhiteSpace($env:FLOWSHARP_REPL_URI) `
        -or -not [string]::IsNullOrWhiteSpace($env:FLOWSHARP_WEBSOCKET_PORT)
}

function Test-FlowSharpEndpoint {
    param(
        [string]$Endpoint,
        [int]$TimeoutSeconds
    )

    $socket = [System.Net.WebSockets.ClientWebSocket]::new()
    $timeout = [Math]::Max(1, $TimeoutSeconds)
    $cts = [Threading.CancellationTokenSource]::new([TimeSpan]::FromSeconds($timeout))

    try {
        $null = $socket.ConnectAsync([Uri]$Endpoint, $cts.Token).GetAwaiter().GetResult()
        $null = $socket.CloseAsync(
            [System.Net.WebSockets.WebSocketCloseStatus]::NormalClosure,
            "ready",
            [Threading.CancellationToken]::None
        ).GetAwaiter().GetResult()
        return $true
    }
    catch {
        return $false
    }
    finally {
        $cts.Dispose()
        $socket.Dispose()
    }
}

function Get-FreeTcpPort {
    $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
    try {
        $listener.Start()
        return ([System.Net.IPEndPoint]$listener.LocalEndpoint).Port
    }
    finally {
        $listener.Stop()
    }
}

function Start-FlowSharpRuntimeControl {
    param(
        [string]$Root,
        [int]$RestPort,
        [int]$WebSocketPort
    )

    $appDir = Join-Path $Root "bin\Debug\net8.0-windows"
    $exePath = Join-Path $appDir "FlowSharp.exe"
    $modulePath = Join-Path $appDir "FlowSharpRuntimeControlModules.xml"

    if (-not (Test-Path -LiteralPath $exePath)) {
        throw "FlowSharp executable was not found: $exePath. Run dotnet build FlowSharp.sln -c Debug first."
    }

    if (-not (Test-Path -LiteralPath $modulePath)) {
        throw "Runtime-control module file was not found: $modulePath. Run dotnet build FlowSharp.sln -c Debug first."
    }

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new($exePath)
    $startInfo.Arguments = '"' + $modulePath + '"'
    $startInfo.WorkingDirectory = $appDir
    $startInfo.UseShellExecute = $false
    $startInfo.Environment["FLOWSHARP_REST_PORT"] = [string]$RestPort
    $startInfo.Environment["FLOWSHARP_WEBSOCKET_PORT"] = [string]$WebSocketPort
    $startInfo.Environment["FLOWSHARP_MACRO_STEP_DELAY_MS"] = "0"

    return [System.Diagnostics.Process]::Start($startInfo)
}

function Wait-FlowSharpEndpoint {
    param(
        [System.Diagnostics.Process]$Process,
        [string]$Endpoint,
        [int]$TimeoutSeconds
    )

    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    while ([DateTime]::UtcNow -lt $deadline) {
        if ($Process.HasExited) {
            throw "FlowSharp exited during startup with code $($Process.ExitCode)."
        }

        if (Test-FlowSharpEndpoint -Endpoint $Endpoint -TimeoutSeconds 1) {
            return
        }

        Start-Sleep -Milliseconds 100
    }

    throw "FlowSharp WebSocket endpoint was not ready at $Endpoint."
}

$scriptDir = Split-Path -Parent $PSCommandPath
$repoRoot = Resolve-Path (Join-Path $scriptDir "..\..\..")
$replPath = Join-Path $repoRoot "tools\FlowSharpRepl.ps1"
$flowPath = Join-Path $scriptDir "08-runtime-feature-surfaces.flow"
$printPath = Join-Path $env:TEMP "flowsharp-runtime-feature-surfaces-print.png"
$diagramPath = Join-Path $env:TEMP "flowsharp-runtime-feature-surfaces.fsd"
$layoutPath = Join-Path $env:TEMP "flowsharp-runtime-feature-surfaces-layout.xml"
$rawResponsePath = Join-Path $env:TEMP ("flowsharp-runtime-feature-surfaces-" + [Guid]::NewGuid().ToString("N") + ".json")
$explicitEndpoint = Test-HasExplicitEndpoint
$Uri = Resolve-FlowSharpUri -Endpoint $Uri
$ownedProcess = $null
$oldRestPort = $env:FLOWSHARP_REST_PORT
$oldWebSocketPort = $env:FLOWSHARP_WEBSOCKET_PORT
$oldMacroDelay = $env:FLOWSHARP_MACRO_STEP_DELAY_MS

try {
    if (Test-Path -LiteralPath $printPath) {
        Remove-Item -LiteralPath $printPath -Force
    }
    if (Test-Path -LiteralPath $diagramPath) {
        Remove-Item -LiteralPath $diagramPath -Force
    }
    if (Test-Path -LiteralPath $layoutPath) {
        Remove-Item -LiteralPath $layoutPath -Force
    }

    if (-not (Test-FlowSharpEndpoint -Endpoint $Uri -TimeoutSeconds $ConnectTimeoutSeconds)) {
        if ($explicitEndpoint) {
            throw "FlowSharp WebSocket endpoint is not reachable at $Uri."
        }

        $restPort = Get-FreeTcpPort
        do {
            $webSocketPort = Get-FreeTcpPort
        }
        while ($webSocketPort -eq $restPort)

        $Uri = "ws://localhost:$webSocketPort/flowsharp/"
        $env:FLOWSHARP_REST_PORT = [string]$restPort
        $env:FLOWSHARP_WEBSOCKET_PORT = [string]$webSocketPort
        $env:FLOWSHARP_MACRO_STEP_DELAY_MS = "0"
        $ownedProcess = Start-FlowSharpRuntimeControl -Root $repoRoot -RestPort $restPort -WebSocketPort $webSocketPort
        Wait-FlowSharpEndpoint -Process $ownedProcess -Endpoint $Uri -TimeoutSeconds 10
    }

    $null = & powershell -ExecutionPolicy Bypass -File $replPath -Uri $Uri -Raw -OutputFile $rawResponsePath -ScriptFile $flowPath -ConnectTimeoutSeconds $ConnectTimeoutSeconds
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $rawResponsePath)) {
        throw "FlowSharp REPL command failed. Ensure FlowSharp is running and listening at $Uri."
    }

    $raw = [System.IO.File]::ReadAllText($rawResponsePath)
    if ([string]::IsNullOrWhiteSpace($raw)) {
        throw "FlowSharp REPL command returned an empty response."
    }

    $parsedResults = ConvertFrom-Json -InputObject $raw
    $results = @($parsedResults | ForEach-Object { $_ })

    foreach ($result in $results) {
        if (-not $result.Success) {
            throw "Macro step $($result.Step) failed: $($result.Error)"
        }
    }

    $textInspect = Get-CommandResponseJson -Results $results -CommandPrefix "inspectshape Name=TextBox "
    $textProps = $textInspect[0].Properties
    Assert-Equal "20,15,130,70" $textProps.TextBounds "TextBounds did not round-trip through inspection."
    Assert-Equal "Justify" $textProps.ParagraphJustification "Paragraph justification was not applied."

    $flowInspect = Get-CommandResponseJson -Results $results -CommandPrefix "inspectshape Name=Flow "
    Assert-Equal "DynamicConnectorLR" $flowInspect[0].Type "Connector conversion did not produce a left-right connector."
    Assert-Equal "12,-8" $flowInspect[0].Properties.LabelOffset "Connector label offset was not applied."

    $aligned = Get-CommandResponseJson -Results $results -CommandPrefix "listshapes Name=AlignB "
    Assert-Equal 100 $aligned[0].X "AlignSelection did not align AlignB to the left edge."

    $snapped = Get-CommandResponseJson -Results $results -CommandPrefix "listshapes Name=SnapMoving "
    Assert-Equal 150 $snapped[0].X "DragSelection did not apply center/edge snapping."

    $customPoints = Get-CommandResponseJson -Results $results -CommandPrefix "inspectshape Name=CustomPointBox " -Occurrence 1
    $custom = @($customPoints[0].ConnectionPoints | Where-Object { $_.IsCustom })[0]
    Assert-Equal 60 $custom.X "Custom connection point X was not resolved in shape coordinates."
    Assert-Equal 40 $custom.Y "Custom connection point Y was not resolved in shape coordinates."

    $resizedCustomPoints = Get-CommandResponseJson -Results $results -CommandPrefix "inspectshape Name=CustomPointBox " -Occurrence 2
    $resizedCustom = @($resizedCustomPoints[0].ConnectionPoints | Where-Object { $_.IsCustom })[0]
    Assert-Equal 110 $resizedCustom.X "Custom connection point X did not remain relative after resize."
    Assert-Equal 60 $resizedCustom.Y "Custom connection point Y did not remain relative after resize."

    $capLine = Get-CommandResponseJson -Results $results -CommandPrefix "inspectshape Name=CapLine "
    Assert-Equal "Square" $capLine[0].Properties.StartCap "Square start cap was not applied."
    Assert-Equal "Round" $capLine[0].Properties.EndCap "Round end cap was not applied."

    $upDown = Get-CommandResponseJson -Results $results -CommandPrefix "inspectshape Name=UpDownCandidate"
    Assert-Equal "DynamicConnectorUD" $upDown[0].Type "Connector conversion did not produce an up-down connector."

    $removeResult = Get-CommandResponseJson -Results $results -CommandPrefix "removediagonalconnectors"
    Assert-Equal 1 $removeResult.Count "RemoveDiagonalConnectors did not report one removed connector."

    $rotateAfter = Get-CommandResponseJson -Results $results -CommandPrefix "inspectshape Name=RotateBox " -Occurrence 1
    Assert-Equal "30" $rotateAfter[0].Properties.RotationAngle "RotateSelection did not snap to 30 degrees."
    $rotateUndo = Get-CommandResponseJson -Results $results -CommandPrefix "inspectshape Name=RotateBox " -Occurrence 2
    Assert-Equal "0" $rotateUndo[0].Properties.RotationAngle "Undo did not restore rotation."
    $rotateRedo = Get-CommandResponseJson -Results $results -CommandPrefix "inspectshape Name=RotateBox " -Occurrence 3
    Assert-Equal "30" $rotateRedo[0].Properties.RotationAngle "Redo did not restore rotation."

    $regrouped = Get-CommandResponseJson -Results $results -CommandPrefix "inspectshape Name=RegroupBox"
    Assert-Equal 2 $regrouped[0].GroupChildCount "RegroupSelection did not restore both group children."

    $routeSourceAfterMove = Get-CommandResponseJson -Results $results -CommandPrefix "inspectshape Name=RouteSource" -Occurrence 2
    $routeTargetAfterMove = Get-CommandResponseJson -Results $results -CommandPrefix "inspectshape Name=RouteTarget" -Occurrence 1
    Assert-Equal "LeftMiddle" $routeSourceAfterMove[0].Connections[0].ShapeGrip "Source grip did not reroute after target moved left."
    Assert-Equal "RightMiddle" $routeTargetAfterMove[0].Connections[0].ShapeGrip "Target grip did not reroute after target moved left."

    $routeSourceAfterResize = Get-CommandResponseJson -Results $results -CommandPrefix "inspectshape Name=RouteSource" -Occurrence 3
    $routeTargetAfterResize = Get-CommandResponseJson -Results $results -CommandPrefix "inspectshape Name=RouteTarget" -Occurrence 2
    Assert-Equal "BottomMiddle" $routeSourceAfterResize[0].Connections[0].ShapeGrip "Source grip did not reroute after target geometry changed."
    Assert-Equal "TopMiddle" $routeTargetAfterResize[0].Connections[0].ShapeGrip "Target grip did not reroute after target geometry changed."

    $viewBeforeFocus = Get-CommandResponseJson -Results $results -CommandPrefix "getcanvasview" -Occurrence 1
    $viewAfterFocus = Get-CommandResponseJson -Results $results -CommandPrefix "getcanvasview" -Occurrence 2
    if ($viewBeforeFocus.ViewportOriginX -eq $viewAfterFocus.ViewportOriginX -and $viewBeforeFocus.ViewportOriginY -eq $viewAfterFocus.ViewportOriginY) {
        throw "ShowShape did not change the viewport origin."
    }

    $persistBefore = Get-CommandResponseJson -Results $results -CommandPrefix "inspectshape Name=PersistBox " -Occurrence 1
    $persistAfter = Get-CommandResponseJson -Results $results -CommandPrefix "inspectshape Name=PersistBox " -Occurrence 2
    Assert-Equal $persistBefore[0].Properties.WordWrap $persistAfter[0].Properties.WordWrap "WordWrap did not persist."
    Assert-Equal $persistBefore[0].Properties.RotationAngle $persistAfter[0].Properties.RotationAngle "RotationAngle did not persist."
    Assert-Equal $persistBefore[0].Properties.TextBounds $persistAfter[0].Properties.TextBounds "TextBounds did not persist."
    Assert-Equal $persistBefore[0].Properties.TextMargin $persistAfter[0].Properties.TextMargin "TextMargin did not persist."
    Assert-Equal $persistBefore[0].Properties.ParagraphJustification $persistAfter[0].Properties.ParagraphJustification "ParagraphJustification did not persist."
    $persistedCustom = @($persistAfter[0].ConnectionPoints | Where-Object { $_.IsCustom })[0]
    Assert-Equal 70 $persistedCustom.X "Persisted custom connection point X was not restored."
    Assert-Equal 680 $persistedCustom.Y "Persisted custom connection point Y was not restored."

    if (-not (Test-Path -LiteralPath $printPath)) {
        throw "Print-page render file was not created: $printPath"
    }
    if (-not (Test-Path -LiteralPath $diagramPath)) {
        throw "Diagram persistence file was not created: $diagramPath"
    }

    Write-Output "Runtime feature surface REPL verification passed."
}
finally {
    $env:FLOWSHARP_REST_PORT = $oldRestPort
    $env:FLOWSHARP_WEBSOCKET_PORT = $oldWebSocketPort
    $env:FLOWSHARP_MACRO_STEP_DELAY_MS = $oldMacroDelay

    if ($ownedProcess -and -not $ownedProcess.HasExited) {
        $ownedProcess.Kill()
        $null = $ownedProcess.WaitForExit(1000)
    }

    if ($ownedProcess) {
        $ownedProcess.Dispose()
    }

    if (Test-Path -LiteralPath $rawResponsePath) {
        Remove-Item -LiteralPath $rawResponsePath -Force
    }
}
