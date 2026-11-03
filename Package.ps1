param([string]$Version='1.1.0')
$ErrorActionPreference='Stop'
if($Version -notmatch '^\d+\.\d+\.\d+([-.][a-zA-Z0-9.]+)?$'){throw 'Use a simple semantic version.'}
& (Join-Path $PSScriptRoot 'Build.ps1')
$wallStage=Join-Path $PSScriptRoot ('.package-stage\'+[Guid]::NewGuid().ToString('N'))
$wallApp=Join-Path $wallStage 'NotesWall'
New-Item -ItemType Directory -Force -Path $wallApp | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'dist\NotesWall.exe') -Destination $wallApp
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'dist\wall.ico') -Destination $wallApp
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'README.md') -Destination $wallApp
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'scripts\创建桌面快捷方式.ps1') -Destination $wallApp
$wallRelease=Join-Path $PSScriptRoot 'releases'
New-Item -ItemType Directory -Force -Path $wallRelease | Out-Null
$wallZip=Join-Path $wallRelease ('NotesWall-v'+$Version+'-win-x64.zip')
Compress-Archive -LiteralPath $wallApp -DestinationPath $wallZip -Force -CompressionLevel Optimal
Add-Type -AssemblyName System.IO.Compression.FileSystem
$wallArchive=[IO.Compression.ZipFile]::OpenRead($wallZip)
try{
 $wallExpected=@('NotesWall.exe','wall.ico','README.md','创建桌面快捷方式.ps1')
 $wallFiles=@($wallArchive.Entries | Where-Object {$_.Name -ne ''})
 if($wallFiles.Count -ne $wallExpected.Count){throw 'Unexpected release contents.'}
 foreach($wallEntry in $wallFiles){if($wallEntry.Name -notin $wallExpected -or $wallEntry.FullName -match '(^|/)data/'){throw 'Unexpected release file.'}}
}finally{$wallArchive.Dispose()}
Write-Output ('Clean release package: '+$wallZip)
