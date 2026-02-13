#Requires -Version 7.0

<#
.SYNOPSIS
    Exports all git-tracked text files into a single dump file for LLM context.
#>

[CmdletBinding()]
param(
    [Parameter()]
    [string]$OutputFile = "llm/dump.txt",
    
    [Parameter()]
    [int]$MaxFileSizeKB = 1024
)

$ErrorActionPreference = 'Stop'

function Test-IsBinaryFile {
    param([string]$FilePath)
    
    $binaryExtensions = @(
        '.exe', '.dll', '.pdb', '.suo', '.user', 
        '.png', '.jpg', '.jpeg', '.gif', '.ico', '.bmp', '.svg',
        '.zip', '.7z', '.tar', '.gz', '.rar',
        '.pdf', '.doc', '.docx', '.xls', '.xlsx',
        '.bin', '.dat', '.db', '.cache'
    )
    
    $extension = [System.IO.Path]::GetExtension($FilePath).ToLower()
    if ($binaryExtensions -contains $extension) {
        return $true
    }
    
    try {
        $bytes = [System.IO.File]::ReadAllBytes($FilePath)
        if ($bytes.Length -eq 0) { return $false }
        
        $sampleSize = [Math]::Min(8192, $bytes.Length)
        $nullBytes = 0
        
        for ($i = 0; $i -lt $sampleSize; $i++) {
            if ($bytes[$i] -eq 0) {
                $nullBytes++
                if ($nullBytes -gt 3) { return $true }
            }
        }
        return $false
    }
    catch {
        return $true
    }
}

try {
    Write-Host "`nFetching git-tracked files..." -ForegroundColor Blue
    
    $gitFiles = git ls-files
    if (-not $gitFiles) {
        throw "No git-tracked files found"
    }
    
    Write-Host "Found $($gitFiles.Count) tracked files" -ForegroundColor Green
    
    $outputDir = Split-Path $OutputFile -Parent
    if ($outputDir -and -not (Test-Path $outputDir)) {
        New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
    }
    
    $outputFileFullPath = if (Test-Path $OutputFile) {
        (Get-Item $OutputFile).FullName
    } else {
        $null
    }
    
    $processedFiles = @()
    $skippedFiles = @()
    $currentPath = (Get-Location).Path
    
    foreach ($file in $gitFiles) {
        $fullPath = Join-Path $currentPath $file
        
        if (-not (Test-Path $fullPath)) {
            $skippedFiles += [PSCustomObject]@{ File = $file; Reason = 'Not found' }
            continue
        }
        
        if ($outputFileFullPath -and (Get-Item $fullPath).FullName -eq $outputFileFullPath) {
            $skippedFiles += [PSCustomObject]@{ File = $file; Reason = 'Output file' }
            continue
        }
        
        $fileSizeKB = [Math]::Round((Get-Item $fullPath).Length / 1KB, 2)
        if ($fileSizeKB -gt $MaxFileSizeKB) {
            $skippedFiles += [PSCustomObject]@{ File = $file; Reason = "Too large ($fileSizeKB KB)" }
            continue
        }
        
        if (Test-IsBinaryFile $fullPath) {
            $skippedFiles += [PSCustomObject]@{ File = $file; Reason = 'Binary' }
            continue
        }
        
        $processedFiles += [PSCustomObject]@{
            File = $file
            FullPath = $fullPath
            SizeKB = $fileSizeKB
        }
        
        Write-Host "  ✓ $file" -ForegroundColor Green
    }
    
    Write-Host "`nGenerating dump file..." -ForegroundColor Blue
    
    $content = [System.Text.StringBuilder]::new()
    [void]$content.AppendLine("=" * 80)
    [void]$content.AppendLine("GIT-TRACKED FILES DUMP")
    [void]$content.AppendLine("Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')")
    [void]$content.AppendLine("Total tracked: $($gitFiles.Count) | Processed: $($processedFiles.Count) | Skipped: $($skippedFiles.Count)")
    [void]$content.AppendLine("=" * 80)
    [void]$content.AppendLine()
    
    foreach ($fileInfo in $processedFiles) {
        try {
            [void]$content.AppendLine()
            [void]$content.AppendLine("=" * 80)
            [void]$content.AppendLine("FILE: $($fileInfo.File)")
            [void]$content.AppendLine("SIZE: $($fileInfo.SizeKB) KB")
            [void]$content.AppendLine("=" * 80)
            
            $fileContent = Get-Content -Path $fileInfo.FullPath -Raw -ErrorAction Stop
            [void]$content.AppendLine($fileContent)
        }
        catch {
            Write-Warning "Failed to read: $($fileInfo.File)"
            [void]$content.AppendLine("ERROR: Could not read file")
        }
    }
    
    $content.ToString() | Set-Content -Path $OutputFile -Encoding UTF8 -NoNewline
    
    $dumpSizeMB = [Math]::Round((Get-Item $OutputFile).Length / 1MB, 2)
    
    Write-Host "`n$('=' * 80)" -ForegroundColor Cyan
    Write-Host "✓ Dump created successfully!" -ForegroundColor Green
    Write-Host "  Output: $OutputFile" -ForegroundColor Cyan
    Write-Host "  Processed: $($processedFiles.Count) files" -ForegroundColor Green
    Write-Host "  Skipped: $($skippedFiles.Count) files" -ForegroundColor Yellow
    Write-Host "  Size: $dumpSizeMB MB" -ForegroundColor Cyan
    Write-Host "$('=' * 80)" -ForegroundColor Cyan
    
    if ($skippedFiles.Count -gt 0) {
        Write-Host "`nSkipped files:" -ForegroundColor Yellow
        $skippedFiles | Format-Table -AutoSize
    }
}
catch {
    Write-Host "`n✗ Error: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
