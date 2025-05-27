# GenerateAIFlowDevchunkPlan.ps1
param (
    [Parameter(Mandatory=$true)]
    [string]$MasterChunkFilePath, # Path to your existing large "newchunk.txt" style file

    [Parameter(Mandatory=$false)]
    [string]$OutputCsvFilePath = ".\aiflow_devchunks_import_plan.csv",

    [Parameter(Mandatory=$false)]
    [int]$FilesPerAIFlowChunk = 5, # Number of source files to group into one AIFlow prepare-input call

    [Parameter(Mandatory=$false)]
    [string]$AIFlowTaskID = "initial_codebase_import",

    [Parameter(Mandatory=$false)]
    [string]$AIFlowHumanRequestGroupID = "hrg_full_codebase_import"
)

Write-Host "AIFlow Devchunk Import Plan Generator"
Write-Host "Master Chunk File: $MasterChunkFilePath"
Write-Host "Output CSV: $OutputCsvFilePath"
Write-Host "Files per AIFlow Chunk: $FilesPerAIFlowChunk"
Write-Host "--------------------------------------------------"

if (-not (Test-Path $MasterChunkFilePath)) {
    Write-Error "Master chunk file not found: $MasterChunkFilePath"
    exit 1
}

# Read the entire content of the master chunk file
$fileContent = Get-Content $MasterChunkFilePath -Raw

# Regex to find all chunk paths
$regex = [regex]'// --- START CHUNK: (.*?)\s*---' # Only need the path for this script
$matches = $regex.Matches($fileContent)

if ($matches.Count -eq 0) {
    Write-Warning "No chunks found in $MasterChunkFilePath. Ensure chunks are formatted correctly, e.g.:"
    Write-Warning "// --- START CHUNK: path/to/file.cs ---"
    exit 1
}

Write-Host "Found $($matches.Count) file definitions in master chunk file."

$allFinalFilePaths = [System.Collections.Generic.List[string]]::new()

foreach ($match in $matches) {
    $relativePath = $match.Groups[1].Value.Trim()
    $finalRelativePath = $relativePath # Start with the original path

    # Apply the same path transformation logic as in CreateHearthenStructure.ps1
    # Prepend "src/" if it's a project file path within a subdirectory
    if ($relativePath.Contains("/")) {
         # It's a file within a project, assume src/ structure
         if (-not $relativePath.StartsWith("src/")) {
            # Check if it's one of the known project prefixes that should be under src/
            $projectPrefixes = @("Hearthen.Domain", "Hearthen.Application", "Hearthen.Infrastructure", "Hearthen.Api", "Hearthen.Shared.Client") # Add any other relevant project prefixes
            $isProjectFileRequiringSrc = $false
            foreach($prefix in $projectPrefixes) {
                if($relativePath.StartsWith($prefix)) {
                    $isProjectFileRequiringSrc = $true
                    break
                }
            }
            if ($isProjectFileRequiringSrc) {
                 $finalRelativePath = Join-Path "src" $relativePath
            }
         }
    }
    # Normalize path separators to forward slashes for consistency
    $allFinalFilePaths.Add($finalRelativePath.Replace("\","/"))
}

Write-Host "Total unique final file paths to process for AIFlow: $($allFinalFilePaths.Count)"

$aiflowDevChunks = [System.Collections.Generic.List[object]]::new()
$currentAIFlowChunkFiles = [System.Collections.Generic.List[string]]::new()
$aiflowPartNumber = 1
$chunkIDCounter = 1

for ($i = 0; $i -lt $allFinalFilePaths.Count; $i++) {
    $currentAIFlowChunkFiles.Add($allFinalFilePaths[$i])

    if ($currentAIFlowChunkFiles.Count -ge $FilesPerAIFlowChunk -or $i -eq ($allFinalFilePaths.Count - 1)) {
        $chunkData = [PSCustomObject]@{
            ChunkID                   = $chunkIDCounter
            AIFlowTaskID              = $AIFlowTaskID
            AIFlowHumanRequestGroupID = $AIFlowHumanRequestGroupID
            AIFlowPartNumber          = $aiflowPartNumber # This is a guide for the human
            FilesToInclude            = $currentAIFlowChunkFiles -join ";"
            Notes                     = "Codebase import - Part $aiflowPartNumber"
        }
        $aiflowDevChunks.Add($chunkData)

        # Reset for next AIFlow chunk
        $currentAIFlowChunkFiles.Clear()
        $aiflowPartNumber++
        $chunkIDCounter++
    }
}

if ($aiflowDevChunks.Count -gt 0) {
    try {
        $aiflowDevChunks | Export-Csv -Path $OutputCsvFilePath -NoTypeInformation -Encoding UTF8
        Write-Host "Successfully generated AIFlow devchunk import plan: $OutputCsvFilePath"
        Write-Host "Each row in the CSV represents a set of files for one 'aiflow-cli prepare-input' command."
        Write-Host "Use the 'AIFlowTaskID' and 'AIFlowHumanRequestGroupID' consistently for all parts."
        Write-Host "The 'AIFlowPartNumber' in the CSV is a human-readable guide; aiflow-cli will manage its own part numbers if a single CSV row's content is too large."
    } catch {
        Write-Error "Failed to write CSV file '$OutputCsvFilePath': $($_.Exception.Message)"
    }
} else {
    Write-Warning "No devchunks were generated. Check the master chunk file and script logic."
}

Write-Host "Generation complete."

