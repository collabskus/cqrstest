#requires -Version 7.0

<#
.SYNOPSIS
    Dumps all git-tracked files into a single text file for LLM analysis
.DESCRIPTION
    Creates llm/dump.txt containing all files that are:
    - In the git index (tracked)
    - Not deleted in the working copy
    - Not ignored by .gitignore
    Excludes bin/, obj/, and other git-ignored files automatically
#>

param(
    [string]$RepoPath = ".",
    [string]$OutputFile = "llm/dump.txt"
)

# Ensure we're in a git repository
Push-Location $RepoPath
try {
    $isGitRepo = git rev-parse --is-inside-work-tree 2>$null
    if ($isGitRepo -ne "true") {
        Write-Error "Not a git repository: $RepoPath"
        exit 1
    }

    # Create output directory if it doesn't exist
    $outputDir = Split-Path $OutputFile -Parent
    if ($outputDir -and -not (Test-Path $outputDir)) {
        New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
        Write-Host "Created directory: $outputDir" -ForegroundColor Green
    }

    # Get all files that are:
    # 1. Tracked by git (in the index)
    # 2. Exist in the working directory (not deleted)
    $trackedFiles = git ls-files | Where-Object {
        Test-Path $_
    }

    if ($trackedFiles.Count -eq 0) {
        Write-Warning "No tracked files found"
        exit 0
    }

    Write-Host "Found $($trackedFiles.Count) tracked files" -ForegroundColor Cyan

    # Create the dump file
    $dumpContent = @()
    $processedCount = 0
    $skippedCount = 0

    foreach ($file in $trackedFiles) {
        # Get absolute path
        $fullPath = Resolve-Path $file -ErrorAction SilentlyContinue
        
        if (-not $fullPath -or -not (Test-Path $fullPath)) {
            $skippedCount++
            continue
        }

        # Try to read the file as text
        try {
            $content = Get-Content -Path $file -Raw -ErrorAction Stop
            
            # Add file separator and content
            $dumpContent += "=" * 80
            $dumpContent += "FILE: $file"
            $dumpContent += "=" * 80
            $dumpContent += $content
            $dumpContent += "`n`n"
            
            $processedCount++
            Write-Host "  ✓ $file" -ForegroundColor Gray
        }
        catch {
            # Skip binary files or files that can't be read as text
            Write-Host "  ✗ Skipped (binary or unreadable): $file" -ForegroundColor Yellow
            $skippedCount++
        }
    }

    # Write to output file
    $dumpContent | Out-File -FilePath $OutputFile -Encoding UTF8 -Force

    Write-Host "`n" + ("=" * 80) -ForegroundColor Green
    Write-Host "✓ Dump created successfully!" -ForegroundColor Green
    Write-Host "  Output: $OutputFile" -ForegroundColor Cyan
    Write-Host "  Processed: $processedCount files" -ForegroundColor Cyan
    Write-Host "  Skipped: $skippedCount files" -ForegroundColor Cyan
    
    $outputFileInfo = Get-Item $OutputFile
    Write-Host "  Size: $([math]::Round($outputFileInfo.Length / 1MB, 2)) MB" -ForegroundColor Cyan
    Write-Host ("=" * 80) -ForegroundColor Green
}
finally {
    Pop-Location
}
