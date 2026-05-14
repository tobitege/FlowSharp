param(
    [string]$Uri,
    [string]$Command,
    [string]$ScriptFile,
    [switch]$Raw,
    [string]$OutputFile,
    [int]$ConnectTimeoutSeconds = 3
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$script:UseRaw = $Raw.IsPresent
$script:OutputFile = $OutputFile

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

function Connect-FlowSharpSocket {
    param(
        [string]$Endpoint,
        [int]$TimeoutSeconds
    )

    $socket = [System.Net.WebSockets.ClientWebSocket]::new()
    $timeout = [Math]::Max(1, $TimeoutSeconds)
    $cts = [Threading.CancellationTokenSource]::new([TimeSpan]::FromSeconds($timeout))

    try {
        $null = $socket.ConnectAsync([Uri]$Endpoint, $cts.Token).GetAwaiter().GetResult()
        return $socket
    }
    catch {
        $socket.Dispose()
        throw "Could not connect to FlowSharp WebSocket endpoint $Endpoint within $timeout second(s)."
    }
    finally {
        $cts.Dispose()
    }
}

function Disconnect-FlowSharpSocket {
    param([System.Net.WebSockets.ClientWebSocket]$Socket)

    if (-not $Socket) {
        return
    }

    try {
        if ($Socket.State -eq [System.Net.WebSockets.WebSocketState]::Open) {
            $null = $Socket.CloseAsync(
                [System.Net.WebSockets.WebSocketCloseStatus]::NormalClosure,
                "Closing",
                [Threading.CancellationToken]::None
            ).GetAwaiter().GetResult()
        }
    }
    finally {
        $Socket.Dispose()
    }
}

function Receive-FlowSharpMessage {
    param([System.Net.WebSockets.ClientWebSocket]$Socket)

    $buffer = New-Object byte[] 4096
    $segment = [ArraySegment[byte]]::new($buffer)
    $builder = [System.Text.StringBuilder]::new()

    while ($true) {
        $result = $Socket.ReceiveAsync($segment, [Threading.CancellationToken]::None).GetAwaiter().GetResult()

        if ($result.MessageType -eq [System.Net.WebSockets.WebSocketMessageType]::Close) {
            return $null
        }

        $null = $builder.Append([Text.Encoding]::UTF8.GetString($buffer, 0, $result.Count))

        if ($result.EndOfMessage) {
            return $builder.ToString()
        }
    }
}

function Send-FlowSharpMessage {
    param(
        [System.Net.WebSockets.ClientWebSocket]$Socket,
        [string]$Payload
    )

    $bytes = [Text.Encoding]::UTF8.GetBytes($Payload)
    $segment = [ArraySegment[byte]]::new($bytes)
    $null = $Socket.SendAsync($segment, [System.Net.WebSockets.WebSocketMessageType]::Text, $true, [Threading.CancellationToken]::None).GetAwaiter().GetResult()
    return Receive-FlowSharpMessage -Socket $Socket
}

function ConvertTo-RunMacroPayload {
    param([string]$Path)

    $resolved = (Resolve-Path $Path).Path
    $escaped = [Uri]::EscapeDataString($resolved)
    return "cmd=runmacro&filename=$escaped"
}

function Format-FlowSharpResponse {
    param(
        [string]$Response,
        [switch]$PassThru
    )

    if ([string]::IsNullOrWhiteSpace($Response)) {
        if ($PassThru) {
            return ""
        }

        Write-Host "(no response)"
        return
    }

    if ($script:UseRaw) {
        if ($PassThru) {
            return $Response
        }

        Write-FlowSharpRawOutput -Text $Response
        return
    }

    $trimmed = $Response.Trim()

    if (($trimmed.StartsWith("{") -or $trimmed.StartsWith("["))) {
        try {
            $formatted = $trimmed | ConvertFrom-Json | ConvertTo-Json -Depth 20

            if ($PassThru) {
                return $formatted
            }

            Write-Host $formatted
            return
        }
        catch {
        }
    }

    if ($PassThru) {
        return $Response
    }

    Write-Host $Response
}

function Write-FlowSharpRawOutput {
    param([string]$Text)

    if ([string]::IsNullOrWhiteSpace($script:OutputFile)) {
        [Console]::Out.Write($Text)
        return
    }

    $encoding = [System.Text.UTF8Encoding]::new($false)
    [System.IO.File]::WriteAllText($script:OutputFile, $Text, $encoding)
}

function Show-Help {
    @"
FlowSharp REPL

Remote commands:
  dropshape ShapeName=Box X=80 Y=80 Name=Start
  listcanvases
  usecanvas Index=1
  selectshapes Name=Start
  moveselection Dx=40 Dy=0
  inspectshape Name=Start Properties=TextAlign,DisplayRectangle

Local commands:
  :help               Show this help
  :quit               Exit
  :reconnect          Reconnect the WebSocket
  :load <file>        Send cmd=runmacro&filename=...
  :raw on|off         Toggle raw output
"@ | Write-Host
}

$socket = $null
$Uri = Resolve-FlowSharpUri -Endpoint $Uri

try {
    $socket = Connect-FlowSharpSocket -Endpoint $Uri -TimeoutSeconds $ConnectTimeoutSeconds

    if ($Command) {
        $response = Send-FlowSharpMessage -Socket $socket -Payload $Command
        Format-FlowSharpResponse -Response $response
        return
    }

    if ($ScriptFile) {
        $payload = ConvertTo-RunMacroPayload -Path $ScriptFile
        $response = Send-FlowSharpMessage -Socket $socket -Payload $payload
        Format-FlowSharpResponse -Response $response
        return
    }

    Show-Help

    while ($true) {
        $line = Read-Host "flowsharp"

        if ([string]::IsNullOrWhiteSpace($line)) {
            continue
        }

        if ($line -match '^:q(uit)?$') {
            break
        }

        switch -Regex ($line) {
            '^:help$' {
                Show-Help
                continue
            }

            '^:reconnect$' {
                Disconnect-FlowSharpSocket -Socket $socket
                $socket = Connect-FlowSharpSocket -Endpoint $Uri
                Write-Host "reconnected"
                continue
            }

            '^:load\s+(.+)$' {
                $payload = ConvertTo-RunMacroPayload -Path $Matches[1]
                $response = Send-FlowSharpMessage -Socket $socket -Payload $payload
                Format-FlowSharpResponse -Response $response
                continue
            }

            '^:raw\s+(on|off)$' {
                if ($Matches[1] -eq "on") {
                    $script:UseRaw = $true
                }
                else {
                    $script:UseRaw = $false
                }

                Write-Host ("raw=" + $script:UseRaw.ToString().ToLowerInvariant())
                continue
            }
        }

        $response = Send-FlowSharpMessage -Socket $socket -Payload $line
        Format-FlowSharpResponse -Response $response
    }
}
finally {
    Disconnect-FlowSharpSocket -Socket $socket
}
