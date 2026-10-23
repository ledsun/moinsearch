[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$Version
)

$ErrorActionPreference = "Stop"

if ($Version -notmatch "^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$") {
    throw "Version must use MAJOR.MINOR.PATCH format, for example 0.0.6."
}

$tag = "v$Version"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$projectRelativePath = "src/Moinsearch/Moinsearch.csproj"
$projectPath = Join-Path $repositoryRoot $projectRelativePath
$projectBytes = [System.IO.File]::ReadAllBytes($projectPath)
$hasUtf8Bom = $projectBytes.Length -ge 3 -and
    $projectBytes[0] -eq 0xEF -and $projectBytes[1] -eq 0xBB -and $projectBytes[2] -eq 0xBF
$projectEncoding = [System.Text.UTF8Encoding]::new($hasUtf8Bom)
$originalProjectContent = [System.IO.File]::ReadAllText($projectPath, $projectEncoding)
$versionPattern = '(?m)(?<start><Version>)[^<]+(?<end></Version>)'
$versionMatches = [System.Text.RegularExpressions.Regex]::Matches($originalProjectContent, $versionPattern)

if ($versionMatches.Count -ne 1) {
    throw "Expected exactly one <Version> element in $projectRelativePath."
}

$currentVersion = [version]($versionMatches[0].Value -replace '<Version>|</Version>', '')
if ([version]$Version -le $currentVersion) {
    throw "Version $Version must be greater than the current project version $currentVersion."
}

Push-Location $repositoryRoot
$projectUpdated = $false
$commitCreated = $false
$tagCreated = $false

try {
    $branch = & git branch --show-current
    if ($LASTEXITCODE -ne 0) {
        throw "Could not determine the current Git branch."
    }
    if ($branch -ne "main") {
        throw "Releases must be created from the main branch (current branch: '$branch')."
    }

    $worktreeStatus = & git status --porcelain
    if ($LASTEXITCODE -ne 0) {
        throw "Could not inspect the Git working tree."
    }
    if (-not [string]::IsNullOrWhiteSpace(($worktreeStatus -join "`n"))) {
        throw "The Git working tree must be clean before creating a release."
    }

    $localTag = & git tag --list $tag
    if ($LASTEXITCODE -ne 0) {
        throw "Could not inspect local Git tags."
    }
    if ($localTag -contains $tag) {
        throw "Tag $tag already exists locally."
    }

    $remoteTag = & git ls-remote --refs origin "refs/tags/$tag"
    if ($LASTEXITCODE -ne 0) {
        throw "Could not inspect remote tags on origin."
    }
    if ($remoteTag) {
        throw "Tag $tag already exists on origin."
    }

    $versionElement = $versionMatches[0]
    $updatedProjectContent = $originalProjectContent.Substring(0, $versionElement.Index) +
        "<Version>$Version</Version>" +
        $originalProjectContent.Substring($versionElement.Index + $versionElement.Length)
    [System.IO.File]::WriteAllText(
        $projectPath,
        $updatedProjectContent,
        $projectEncoding
    )
    $projectUpdated = $true

    & dotnet restore moinsearch.slnx --locked-mode
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet restore failed with exit code $LASTEXITCODE."
    }

    & dotnet test moinsearch.slnx --configuration Release --no-restore
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet test failed with exit code $LASTEXITCODE."
    }

    & git diff --check
    if ($LASTEXITCODE -ne 0) {
        throw "git diff --check failed."
    }

    & git add -- $projectRelativePath
    if ($LASTEXITCODE -ne 0) {
        throw "Could not stage the project version update."
    }

    & git commit -m "chore: release $tag"
    if ($LASTEXITCODE -ne 0) {
        throw "Could not create the release version commit."
    }
    $commitCreated = $true

    & git tag --annotate $tag --message "Release $tag"
    if ($LASTEXITCODE -ne 0) {
        throw "The release commit was created, but tag $tag could not be created."
    }
    $tagCreated = $true

    & git push --atomic origin "HEAD:refs/heads/main" "refs/tags/${tag}:refs/tags/${tag}"
    if ($LASTEXITCODE -ne 0) {
        throw "The release commit and tag exist locally, but the atomic push failed."
    }

    Write-Host "Released $tag."
}
catch {
    if ($projectUpdated -and -not $commitCreated) {
        & git restore --staged -- $projectRelativePath
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "Could not unstage $projectRelativePath; restore it manually."
        }
        [System.IO.File]::WriteAllText(
            $projectPath,
            $originalProjectContent,
            $projectEncoding
        )
    }

    if ($commitCreated -and -not $tagCreated) {
        throw "The release commit exists locally without its tag. $($_.Exception.Message)"
    }
    if ($tagCreated) {
        throw "The release commit and tag exist locally; check the remote before retrying. $($_.Exception.Message)"
    }
    throw
}
finally {
    Pop-Location
}
