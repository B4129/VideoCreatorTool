# Version Update Script for VideoCreator
# Automatically increments the build number (4th digit) of the version
# Usage: .\scripts\UpdateVersion.ps1

param(
    [string]$ProjectFile = ".\VideoCreator.csproj"
)

# Read the project file
$content = Get-Content $ProjectFile -Raw

# Extract current version (pattern: <Version>X.Y.Z.W</Version>)
if ($content -match '<Version>(\d+)\.(\d+)\.(\d+)\.(\d+)</Version>') {
    $major = [int]$matches[1]
    $minor = [int]$matches[2]
    $patch = [int]$matches[3]
    $build = [int]$matches[4]

    # Increment build number
    $build++

    $newVersion = "$major.$minor.$patch.$build"
    $oldVersionPattern = "<Version>\d+\.\d+\.\d+\.\d+</Version>"
    $newVersionTag = "<Version>$newVersion</Version>"

    # Also update AssemblyVersion, FileVersion, and InformationalVersion
    $content = $content -replace $oldVersionPattern, $newVersionTag
    $content = $content -replace '<AssemblyVersion>\d+\.\d+\.\d+\.\d+</AssemblyVersion>', "<AssemblyVersion>$newVersion</AssemblyVersion>"
    $content = $content -replace '<FileVersion>\d+\.\d+\.\d+\.\d+</FileVersion>', "<FileVersion>$newVersion</FileVersion>"
    $content = $content -replace '<InformationalVersion>[\d\.]+(-.*?)?</InformationalVersion>', "<InformationalVersion>$newVersion-dev</InformationalVersion>"

    # Write back to project file
    Set-Content $ProjectFile -Value $content -NoNewline

    Write-Host "Version updated to: $newVersion"
} else {
    Write-Host "Version tag not found in project file!"
    exit 1
}
