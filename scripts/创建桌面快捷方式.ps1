$ErrorActionPreference='Stop'
$wallExe=Join-Path $PSScriptRoot 'NotesWall.exe'
if(-not (Test-Path -LiteralPath $wallExe)){throw 'NotesWall.exe is missing.'}
$wallDesktop=[Environment]::GetFolderPath('Desktop')
$wallShell=New-Object -ComObject WScript.Shell
$wallShortcut=$wallShell.CreateShortcut((Join-Path $wallDesktop '便签墙.lnk'))
$wallShortcut.TargetPath=$wallExe
$wallShortcut.WorkingDirectory=$PSScriptRoot
$wallShortcut.IconLocation=(Join-Path $PSScriptRoot 'wall.ico')+',0'
$wallShortcut.Description='主题分类、课程作业、考试日期和提醒'
$wallShortcut.Save()
Write-Output 'Desktop shortcut created.'
