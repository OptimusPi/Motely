param(
	[string]$ExtensionVersion,
	[switch]$SkipInstall
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
$propsPath = Join-Path $root 'Directory.Packages.props'
$extensionDir = Join-Path $root 'tools\jaml-language\vscode-extension'
$extensionPackageJson = Join-Path $extensionDir 'package.json'
$extensionReadme = Join-Path $extensionDir 'README.md'

function Write-Step([string]$Message) {
	Write-Host "`n==> $Message" -ForegroundColor Cyan
}

function Get-MotelyVersion {
	$xml = [xml](Get-Content -Path $propsPath -Raw)
	$version = $xml.Project.PropertyGroup.MotelyVersion
	if ([string]::IsNullOrWhiteSpace($version)) {
		throw "Could not find <MotelyVersion> in $propsPath"
	}
	return $version.Trim()
}

function Save-JsonFile([string]$Path, [object]$Value) {
	$json = $Value | ConvertTo-Json -Depth 100
	[System.IO.File]::WriteAllText($Path, $json + [Environment]::NewLine)
}

$motelyVersion = Get-MotelyVersion

Write-Step 'Sync VS Code extension package metadata'
$package = Get-Content -Path $extensionPackageJson -Raw | ConvertFrom-Json
$currentExtensionVersion = [string]$package.version
$targetExtensionVersion = if ($PSBoundParameters.ContainsKey('ExtensionVersion')) {
	$ExtensionVersion
}
else {
	$currentExtensionVersion
}

$package.version = $targetExtensionVersion
Save-JsonFile -Path $extensionPackageJson -Value $package

$readme = Get-Content -Path $extensionReadme -Raw
$readme = [regex]::Replace(
	$readme,
	'jaml-language-support-\d+\.\d+\.\d+\.vsix',
	"jaml-language-support-$targetExtensionVersion.vsix"
)
[System.IO.File]::WriteAllText($extensionReadme, $readme)

Write-Host "Extension version: $currentExtensionVersion -> $targetExtensionVersion"
Write-Host "MotelyVersion (repo): $motelyVersion"

Push-Location (Join-Path $root 'tools\jaml-language')
try {
	if (-not $SkipInstall) {
		Write-Step 'Refresh pnpm lockfile and install extension dependencies'
		pnpm --filter ./vscode-extension install
		if ($LASTEXITCODE -ne 0) {
			throw 'pnpm install failed'
		}
	}

	Write-Step 'Package VS Code extension'
	pnpm --filter ./vscode-extension run package
	if ($LASTEXITCODE -ne 0) {
		throw 'VSIX packaging failed'
	}
}
finally {
	Pop-Location
}

$latestVsix = Get-ChildItem -Path $extensionDir -Filter '*.vsix' |
	Sort-Object LastWriteTime -Descending |
	Select-Object -First 1

if ($null -eq $latestVsix) {
	throw "Packaging finished but no .vsix file was found in $extensionDir"
}

Write-Host ''
Write-Host 'Done. Latest VSIX:'
Write-Host $latestVsix.FullName
