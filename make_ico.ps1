Add-Type -AssemblyName System.Drawing
$pngPath = Join-Path $PSScriptRoot "az5_button.png"
$icoPath = Join-Path $PSScriptRoot "app.ico"

if (Test-Path $pngPath) {
    Write-Host "Loading PNG from $pngPath..."
    $srcImg = [System.Drawing.Image]::FromFile($pngPath)
    
    # Create a 256x256 canvas
    $bmp = New-Object System.Drawing.Bitmap(256, 256)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    
    # Draw original image scaled to 256x256
    $g.DrawImage($srcImg, 0, 0, 256, 256)
    $g.Dispose()
    $srcImg.Dispose()
    
    # Save resized to memory stream
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngBytes = $ms.ToArray()
    $ms.Dispose()
    $bmp.Dispose()
    
    # Write ICO file structure
    $fs = New-Object System.IO.FileStream($icoPath, [System.IO.FileMode]::Create)
    $bw = New-Object System.IO.BinaryWriter($fs)
    
    # Write ICONDIR header (6 bytes)
    $bw.Write([UInt16]0) # Reserved (must be 0)
    $bw.Write([UInt16]1) # Resource Type (1 for Icon)
    $bw.Write([UInt16]1) # Number of Images
    
    # Write ICONDIRENTRY (16 bytes)
    $bw.Write([Byte]0)   # Width (0 means 256)
    $bw.Write([Byte]0)   # Height (0 means 256)
    $bw.Write([Byte]0)   # Color count (0 if >= 8bpp)
    $bw.Write([Byte]0)   # Reserved (must be 0)
    $bw.Write([UInt16]1) # Color Planes (1)
    $bw.Write([UInt16]32)# Bits per pixel (32)
    $bw.Write([UInt32]$pngBytes.Length) # Image data size
    $bw.Write([UInt32]22) # Offset to image data (6 header + 16 entry = 22)
    
    # Write PNG image bytes
    $bw.Write($pngBytes, 0, $pngBytes.Length)
    
    $bw.Close()
    $fs.Close()
    Write-Host "Successfully generated ICO file at $icoPath"
} else {
    Write-Error "Source PNG not found at $pngPath"
}
