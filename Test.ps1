param([switch]$IncludeUI)
$ErrorActionPreference='Stop'
& (Join-Path $PSScriptRoot 'Build.ps1')
$wallExe=Join-Path $PSScriptRoot 'dist\NotesWall.exe'
$wallTestRoot=Join-Path $PSScriptRoot ('.test-output\'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $wallTestRoot | Out-Null
function Invoke-WallCheck([string]$Mode,[string]$Folder,[string]$ResultFile){
 $wallOutput=Join-Path $wallTestRoot $Folder
 $wallArgs=$Mode+' "'+$wallOutput+'"'
 $wallRun=Start-Process -FilePath $wallExe -ArgumentList $wallArgs -WindowStyle Hidden -PassThru
 if(-not $wallRun.WaitForExit(30000)){$wallRun.Kill();throw "Timed out: $Mode"}
 $wallResult=Join-Path $wallOutput $ResultFile
 if(Test-Path -LiteralPath $wallResult){Get-Content -LiteralPath $wallResult}
 if($wallRun.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $wallResult)){throw "Failed: $Mode"}
}
Invoke-WallCheck '--self-test' 'core' 'core-test.txt'
if($IncludeUI){Invoke-WallCheck '--ui-test' 'ui' 'ui-test.txt'}
Write-Output 'All requested checks passed.'
