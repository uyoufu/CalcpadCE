<#
.SYNOPSIS
    Batch replace text in all .cpdz files under a directory.

.DESCRIPTION
    Reads each .cpdz file, decompresses the code content, replaces the search
    string, then recompresses and saves. Supports both composite (ZIP) and
    simple (raw DeflateStream) .cpdz formats as used by Calcpad.

.PARAMETER Directory
    The root directory to scan for .cpdz files.

.PARAMETER SearchString
    The text to find.

.PARAMETER ReplaceString
    The replacement text.

.PARAMETER Recurse
    Search subdirectories recursively.

.PARAMETER DryRun
    Show what would be changed without modifying any files.

.EXAMPLE
    .\Replace-CpdzText.ps1 -Directory "C:\Docs" -SearchString "old" -ReplaceString "new" -Recurse
#>

param(
    [Parameter(Mandatory = $true)]
    [string]$Directory,

    [Parameter(Mandatory = $true)]
    [string]$SearchString,

    [Parameter(Mandatory = $true)]
    [string]$ReplaceString,

    [switch]$Recurse,

    [switch]$DryRun
)

Add-Type -AssemblyName System.IO.Compression

function Read-DeflateString([System.IO.Stream]$Stream) {
    $ms = New-Object System.IO.MemoryStream
    $ds = New-Object System.IO.Compression.DeflateStream($Stream, [System.IO.Compression.CompressionMode]::Decompress, $true)
    $ds.CopyTo($ms)
    $ds.Close()
    $ms.Position = 0
    $sr = New-Object System.IO.StreamReader($ms, [System.Text.Encoding]::UTF8)
    $text = $sr.ReadToEnd()
    $sr.Close()
    $ms.Close()
    return $text
}

function Write-DeflateString([string]$Text, [System.IO.Stream]$Stream) {
    $ms = New-Object System.IO.MemoryStream
    $sw = New-Object System.IO.StreamWriter($ms, [System.Text.Encoding]::UTF8)
    $sw.Write($Text)
    $sw.Flush()
    $ms.Position = 0
    $ds = New-Object System.IO.Compression.DeflateStream($Stream, [System.IO.Compression.CompressionMode]::Compress, $true)
    $ms.CopyTo($ds)
    $ds.Close()
    $ms.Close()
}

function Test-CompositeZip([string]$FilePath) {
    $bytes = [System.IO.File]::ReadAllBytes($FilePath)
    if ($bytes.Length -lt 2) { return $false }
    return ($bytes[0] -eq 0x50 -and $bytes[1] -eq 0x4B)
}

function Process-CompositeCpdz([string]$FilePath, [string]$Search, [string]$Replace) {
    $bytes = [System.IO.File]::ReadAllBytes($FilePath)
    $ms = New-Object System.IO.MemoryStream(, $bytes)
    $archive = New-Object System.IO.Compression.ZipArchive($ms, [System.IO.Compression.ZipArchiveMode]::Read)

    $originalText = $null
    foreach ($entry in $archive.Entries) {
        if ($entry.Name -eq "code.cpd") {
            $entryStream = $entry.Open()
            $originalText = Read-DeflateString $entryStream
            $entryStream.Close()
            break
        }
    }
    $archive.Dispose()
    $ms.Dispose()

    if ($null -eq $originalText) {
        Write-Host "  [SKIP] No code.cpd entry found." -ForegroundColor Yellow
        return
    }

    $newText = $originalText.Replace($Search, $Replace)
    if ($originalText -eq $newText) {
        Write-Host "  [SKIP] No matches found." -ForegroundColor Gray
        return
    }

    $matchCount = ([regex]::Escape($Search) -eq $Search) ?
        (($originalText.Split([string]$Search).Count - 1)) :
        ([regex]::Matches($originalText, [regex]::Escape($Search)).Count)
    Write-Host "  [OK] Replaced $matchCount occurrence(s)." -ForegroundColor Green

    if ($DryRun) { return }

    # Re-read original ZIP to copy non-code entries
    $bytes = [System.IO.File]::ReadAllBytes($FilePath)
    $ms = New-Object System.IO.MemoryStream(, $bytes)
    $srcArchive = New-Object System.IO.Compression.ZipArchive($ms, [System.IO.Compression.ZipArchiveMode]::Read)

    $fs = New-Object System.IO.FileStream($FilePath, [System.IO.FileMode]::Create)
    $dstArchive = New-Object System.IO.Compression.ZipArchive($fs, [System.IO.Compression.ZipArchiveMode]::Create)

    foreach ($entry in $srcArchive.Entries) {
        if ($entry.Name -eq "code.cpd") {
            $newEntry = $dstArchive.CreateEntry("code.cpd", [System.IO.Compression.CompressionLevel]::Fastest)
            $entryStream = $newEntry.Open()
            Write-DeflateString $newText $entryStream
            $entryStream.Close()
        }
        elseif ($entry.Length -gt 0) {
            $newEntry = $dstArchive.CreateEntry($entry.FullName, [System.IO.Compression.CompressionLevel]::Fastest)
            $dstStream = $newEntry.Open()
            $srcStream = $entry.Open()
            $srcStream.CopyTo($dstStream)
            $srcStream.Close()
            $dstStream.Close()
        }
    }

    $dstArchive.Dispose()
    $fs.Close()
    $srcArchive.Dispose()
    $ms.Dispose()
}

function Process-SimpleCpdz([string]$FilePath, [string]$Search, [string]$Replace) {
    $fs = New-Object System.IO.FileStream($FilePath, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read)
    $originalText = Read-DeflateString $fs
    $fs.Close()

    $newText = $originalText.Replace($Search, $Replace)
    if ($originalText -eq $newText) {
        Write-Host "  [SKIP] No matches found." -ForegroundColor Gray
        return
    }

    $matchCount = ($originalText.Split([string]$Search).Count - 1)
    Write-Host "  [OK] Replaced $matchCount occurrence(s)." -ForegroundColor Green

    if ($DryRun) { return }

    $fs = New-Object System.IO.FileStream($FilePath, [System.IO.FileMode]::Create, [System.IO.FileAccess]::Write)
    Write-DeflateString $newText $fs
    $fs.Close()
}

# --- Main ---

$files = if ($Recurse) {
    Get-ChildItem -Path $Directory -Filter "*.cpdz" -Recurse -File
}
else {
    Get-ChildItem -Path $Directory -Filter "*.cpdz" -File
}

if ($files.Count -eq 0) {
    Write-Host "No .cpdz files found in '$Directory'." -ForegroundColor Yellow
    exit 0
}

Write-Host "Found $($files.Count) .cpdz file(s). Search: '$SearchString' -> '$ReplaceString'" -ForegroundColor Cyan
if ($DryRun) { Write-Host "[DRY RUN] No files will be modified." -ForegroundColor Yellow }
Write-Host ""

$updated = 0
foreach ($file in $files) {
    Write-Host "[$($updated + 1)/$($files.Count)] $($file.FullName)"
    if (Test-CompositeZip $file.FullName) {
        Process-CompositeCpdz $file.FullName $SearchString $ReplaceString
    }
    else {
        Process-SimpleCpdz $file.FullName $SearchString $ReplaceString
    }
    $updated++
}

Write-Host "`nDone." -ForegroundColor Cyan
