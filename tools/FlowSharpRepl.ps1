param(
    [string]$Uri = "ws://localhost:1100/flowsharp/",
    [string]$Command,
    [string]$ScriptFile,
    [switch]$Raw
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$script:UseRaw = $Raw.IsPresent

function Connect-FlowSharpSocket {
    param([string]$Endpoint)

    $socket = [System.Net.WebSockets.ClientWebSocket]::new()
    $socket.ConnectAsync([Uri]$Endpoint, [Threading.CancellationToken]::None).GetAwaiter().GetResult()
    return $socket
}

function Disconnect-FlowSharpSocket {
    param([System.Net.WebSockets.ClientWebSocket]$Socket)

    if (-not $Socket) {
        return
    }

    try {
        if ($Socket.State -eq [System.Net.WebSockets.WebSocketState]::Open) {
            $Socket.CloseAsync(
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
    $Socket.SendAsync($segment, [System.Net.WebSockets.WebSocketMessageType]::Text, $true, [Threading.CancellationToken]::None).GetAwaiter().GetResult()
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

        Write-Host $Response
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

try {
    $socket = Connect-FlowSharpSocket -Endpoint $Uri

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
