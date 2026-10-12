<#
.SYNOPSIS
	Builds SFallConfigurator as two standalone executables in the "publish" folder.

.DESCRIPTION
	SFallConfigurator.exe      The program alone (about 1 MB). It needs the .NET 10 Desktop Runtime on the computer.
	SFallConfigurator-NET.exe  The program with the .NET runtime inside (about 60 MB). Nothing has to be installed.

	Each file is a single executable. The previous "publish" folder is replaced only when both builds succeeded.

.EXAMPLE
	.\Publish.ps1
	.\Publish.ps1 -Runtime win-arm64
#>
[CmdletBinding()]
param(
	[ValidateSet('Release', 'Debug')]
	[string]$Configuration = 'Release',

	[ValidateSet('win-x64', 'win-x86', 'win-arm64')]
	[string]$Runtime = 'win-x64'
)

$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
$project = Join-Path $root 'SFallConfigurator\SFallConfigurator.csproj'
$output = Join-Path $root 'publish'
$staging = Join-Path $root 'publish.tmp'

$variants = @(
	@{ Name = 'SFallConfigurator.exe'; SelfContained = $false },
	@{ Name = 'SFallConfigurator-NET.exe'; SelfContained = $true }
)

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
	throw 'The .NET SDK was not found. Install the .NET 10 SDK.'
}

function Remove-Folder([string]$path) {
	if (Test-Path $path) {
		Remove-Item $path -Recurse -Force
	}
}

# The builds go to a temporary folder: the previous publish is replaced only when the new ones succeeded.
Remove-Folder $staging

try {
	foreach ($variant in $variants) {
		$selfContained = if ($variant.SelfContained) { 'true' } else { 'false' }
		$folder = Join-Path $staging ([System.IO.Path]::GetFileNameWithoutExtension($variant.Name))

		$arguments = @(
			'publish', $project,
			'--configuration', $Configuration,
			'--runtime', $Runtime,
			'--self-contained', $selfContained,
			'--output', $folder,
			'-p:PublishSingleFile=true',
			'-p:DebugType=None',
			'-p:DebugSymbols=false'
		)

		if ($variant.SelfContained) {
			# The WPF native libraries are packed into the file and unpacked on first start.
			$arguments += '-p:IncludeNativeLibrariesForSelfExtract=true'
			$arguments += '-p:EnableCompressionInSingleFile=true'
		}

		Write-Host "Publishing $($variant.Name) ($Configuration, $Runtime, self-contained: $selfContained)..." -ForegroundColor Cyan
		& dotnet @arguments
		if ($LASTEXITCODE -ne 0) {
			throw "dotnet publish failed with exit code $LASTEXITCODE for $($variant.Name)."
		}
	}

	# The new folder is complete before the old one is touched.
	$ready = Join-Path $root 'publish.new'
	Remove-Folder $ready
	New-Item -ItemType Directory -Path $ready | Out-Null

	foreach ($variant in $variants) {
		$folder = Join-Path $staging ([System.IO.Path]::GetFileNameWithoutExtension($variant.Name))
		Move-Item (Join-Path $folder 'SFallConfigurator.exe') (Join-Path $ready $variant.Name)
	}

	# No file of an older publish is left behind. The old folder is only moved aside first: when that fails (an
	# executable of the folder is running), the old publish is still there and nothing is lost.
	$previous = Join-Path $root 'publish.old'
	Remove-Folder $previous
	if (Test-Path $output) {
		Move-Item $output $previous
	}

	try {
		Move-Item $ready $output
	}
	catch {
		if (Test-Path $previous) {
			Move-Item $previous $output
		}

		throw
	}

	Remove-Folder $previous
}
finally {
	Remove-Folder $staging
	Remove-Folder (Join-Path $root 'publish.new')
}

foreach ($variant in $variants) {
	$exe = Get-Item (Join-Path $output $variant.Name)
	$size = [math]::Round($exe.Length / 1MB, 1)
	Write-Host "Done: $($exe.FullName) ($size MB)" -ForegroundColor Green
}
