$systemRoot = if ($env:SystemRoot) { $env:SystemRoot } else { "C:\Windows" }
$cscPath = "$systemRoot\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $cscPath)) {
    $cscPath = "$systemRoot\Microsoft.NET\Framework\v4.0.30319\csc.exe"
}
if (-not (Test-Path $cscPath)) {
    $cmd = Get-Command csc.exe -ErrorAction SilentlyContinue
    if ($cmd) { $cscPath = $cmd.Source }
}
if (-not (Test-Path $cscPath)) {
    Write-Error "C# compiler (csc.exe) not found on this system."
    exit 1
}

Write-Host "Compiling AZ-5 Button.exe..."
# Compile options:
# /target:winexe - makes it a Windows GUI app (no console window popup)
# /out:"AZ-5 Button.exe" - output executable name
# /win32icon:app.ico - embeds the icon for explorer / taskbar
# /resource:az5_button.png - embeds the background image
& $cscPath /target:winexe /out:"AZ-5 Button.exe" /win32icon:app.ico /resource:az5_button.png Program.cs

if ($LASTEXITCODE -eq 0) {
    Write-Host "Compilation successful! Standalone executable generated at: $(Get-Item '.\AZ-5 Button.exe' | Select-Object -ExpandProperty FullName)"
} else {
    Write-Error "Compilation failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}
