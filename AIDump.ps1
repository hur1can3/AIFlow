# AIDump.ps1
param (
    [Parameter(Mandatory=$true)]
    [string]$ChunkFilePath = ".\newchunk.txt",

    [Parameter(Mandatory=$false)]
    [string]$SolutionRoot = (Get-Location).Path
)

Write-Host "Project Structure and File Populator"
Write-Host "Solution Root: $SolutionRoot"
Write-Host "Chunk File: $ChunkFilePath"
Write-Host "--------------------------------------------------"

if (-not (Test-Path $ChunkFilePath)) {
    Write-Error "Chunk file not found: $ChunkFilePath"
    exit 1
}

# Read the entire content of the chunk file
$fileContent = Get-Content $ChunkFilePath -Raw

# Regex to find all chunks
$regex = [regex]'(?s)// --- START CHUNK: (.*?)\s*---\s*(.*?)\s*// --- END CHUNK: \1\s*---'
$matches = $regex.Matches($fileContent)

if ($matches.Count -eq 0) {
    Write-Warning "No chunks found in $ChunkFilePath. Ensure chunks are formatted correctly, e.g.:"
    Write-Warning "// --- START CHUNK: path/to/file.cs ---"
    Write-Warning "// ... code ..."
    Write-Warning "// --- END CHUNK: path/to/file.cs ---"
    exit 1
}

Write-Host "Found $($matches.Count) chunk(s) to process."

foreach ($match in $matches) {
    $relativePath = $match.Groups[1].Value.Trim()
    $codeContent = $match.Groups[2].Value.TrimStart() # Trim leading newlines from content

    Write-Host "Processing chunk for: $relativePath"

    # Prepend "src/" if it's a project file path, unless it's a root file
    $finalRelativePath = $relativePath
    if (($relativePath.StartsWith("Hearthen.Domain") -or `
         $relativePath.StartsWith("Hearthen.Application") -or `
         $relativePath.StartsWith("Hearthen.Infrastructure") -or `
         $relativePath.StartsWith("Hearthen.Api") -or `
         $relativePath.StartsWith("Hearthen.Shared.Client")) -and `
         -not $relativePath.Contains("/") # Simple check for root project files like csproj.txt
        ) {
        # This logic might need refinement if paths get more complex than simple root files
        # For now, this handles files directly in project roots vs. subfolders.
    } elseif ($relativePath.Contains("/")) {
         # It's a file within a project, assume src/ structure
         if (-not $relativePath.StartsWith("src/")) {
            # Check if it's one of the known project prefixes that should be under src/
            $projectPrefixes = @("Hearthen.Domain", "Hearthen.Application", "Hearthen.Infrastructure", "Hearthen.Api", "Hearthen.Shared.Client")
            $isProjectFile = $false
            foreach($prefix in $projectPrefixes) {
                if($relativePath.StartsWith($prefix)) {
                    $isProjectFile = $true
                    break
                }
            }
            if ($isProjectFile) {
                 $finalRelativePath = Join-Path "src" $relativePath
            }
         }
    }


    $targetFilePath = Join-Path -Path $SolutionRoot -ChildPath $finalRelativePath
    $targetDirectory = Split-Path -Path $targetFilePath

    # Create directory if it doesn't exist
    if (-not (Test-Path $targetDirectory)) {
        Write-Host "Creating directory: $targetDirectory"
        try {
            New-Item -ItemType Directory -Path $targetDirectory -Force -ErrorAction Stop | Out-Null
        } catch {
            Write-Error "Failed to create directory '$targetDirectory': $($_.Exception.Message)"
            continue # Skip to next chunk
        }
    }

    # Write content to file
    try {
        Set-Content -Path $targetFilePath -Value $codeContent -Encoding UTF8 -ErrorAction Stop
        Write-Host "Successfully wrote: $targetFilePath"
    } catch {
        Write-Error "Failed to write file '$targetFilePath': $($_.Exception.Message)"
    }
    Write-Host "---"
}

Write-Host "Processing complete."
Write-Host "Remember to create the .sln and .csproj files using 'dotnet new' commands and 'dotnet sln add ...'"
Write-Host "You can use the *.csproj.txt files I provide as a guide for your project references."

