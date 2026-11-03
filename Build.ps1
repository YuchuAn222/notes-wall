$ErrorActionPreference='Stop'
$wallFramework=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$wallCompiler=Join-Path $wallFramework 'csc.exe'
if(-not (Test-Path -LiteralPath $wallCompiler)){throw 'The .NET Framework 4.x x64 C# compiler is required.'}
$wallDist=Join-Path $PSScriptRoot 'dist'
New-Item -ItemType Directory -Force -Path $wallDist | Out-Null
$wallRefs=@('PresentationCore','PresentationFramework','WindowsBase','UIAutomationTypes') | ForEach-Object {'/r:'+(Join-Path $wallFramework "WPF\$_.dll")}
$wallSources=@('Model.cs','UI.cs','NotesWall.cs','Checks.cs') | ForEach-Object {Join-Path $PSScriptRoot ('src\'+$_)}
& $wallCompiler /nologo /target:winexe /platform:x64 /optimize+ /main:WallProgram "/out:$wallDist\NotesWall.exe" $wallRefs /r:System.Xaml.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Xml.dll /r:System.Core.dll "/win32manifest:$PSScriptRoot\src\app.manifest" "/win32icon:$PSScriptRoot\assets\wall.ico" $wallSources
if($LASTEXITCODE){throw 'Build failed.'}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'assets\wall.ico') -Destination (Join-Path $wallDist 'wall.ico') -Force
Write-Output 'Build complete: dist/NotesWall.exe'
