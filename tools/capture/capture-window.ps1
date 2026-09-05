<#
.SYNOPSIS
  Launches a Unity Windows player in a borderless window, captures its client area to PNG
  frames, then closes it. Used to produce the README screenshots and GIF frames without a
  human at the keyboard.

.EXAMPLE
  pwsh tools/capture/capture-window.ps1 -Exe Builds/Windows/Match3Lab.exe -OutDir docs/media/frames -Frames 40 -IntervalMs 120 -PlayerArgs "-autoplay"
#>
param(
    [Parameter(Mandatory = $true)] [string] $Exe,
    [Parameter(Mandatory = $true)] [string] $OutDir,
    [int] $Width = 720,
    [int] $Height = 1280,
    [double] $WaitSeconds = 5,
    [int] $Frames = 1,
    [int] $IntervalMs = 150,
    [string] $PlayerArgs = ""
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public static class Win32 {
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
    [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr hWnd, ref POINT point);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
}
"@
[void][Win32]::SetProcessDPIAware()

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$argList = "-screen-width $Width -screen-height $Height -screen-fullscreen 0 -popupwindow $PlayerArgs"
$proc = Start-Process -FilePath (Resolve-Path $Exe) -ArgumentList $argList -PassThru
try {
    Start-Sleep -Seconds $WaitSeconds
    $proc.Refresh()
    $deadline = (Get-Date).AddSeconds(15)
    while ($proc.MainWindowHandle -eq 0 -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 200; $proc.Refresh() }
    if ($proc.MainWindowHandle -eq 0) { throw "player window did not appear" }
    $h = $proc.MainWindowHandle
    [void][Win32]::SetForegroundWindow($h)
    Start-Sleep -Milliseconds 300

    $rect = New-Object Win32+RECT
    [void][Win32]::GetClientRect($h, [ref]$rect)
    $origin = New-Object Win32+POINT
    [void][Win32]::ClientToScreen($h, [ref]$origin)
    $w = $rect.Right - $rect.Left
    $hgt = $rect.Bottom - $rect.Top
    if ($w -le 0 -or $hgt -le 0) { throw "empty client rect" }

    for ($i = 0; $i -lt $Frames; $i++) {
        $bmp = New-Object System.Drawing.Bitmap $w, $hgt
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $g.CopyFromScreen($origin.X, $origin.Y, 0, 0, $bmp.Size)
        $g.Dispose()
        $path = Join-Path $OutDir ("frame_{0:D3}.png" -f $i)
        $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
        $bmp.Dispose()
        if ($i -lt $Frames - 1) { Start-Sleep -Milliseconds $IntervalMs }
    }
    Write-Output ("captured {0} frame(s) of {1}x{2} into {3}" -f $Frames, $w, $hgt, $OutDir)
}
finally {
    if (-not $proc.HasExited) { Stop-Process -Id $proc.Id -Force }
}
