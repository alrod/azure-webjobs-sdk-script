param (
	[string]$toolNupkgUrl = "https://functionsperfst.blob.core.windows.net/test/WebJobs.Script.Performance.App.1.0.0.nupkg",
    [string]$toolArgs = "-r https://ci.appveyor.com/api/buildjobs/ax3jch5m0d57hdkm/artifacts/Functions.Private.2.0.12165.win-x32.inproc.zip"
)

$location = Get-Location
$currentTime = "$((Get-Date).ToUniversalTime().ToString('yyyy-MM-dd HH_mm_ss'))"
Start-Transcript -path "$location\run$currentTime.log" -append

Write-Output "Tool: $toolNupkgUrl"
Write-Output "Args: $toolArgs"

if ([string]::IsNullOrEmpty($nupkgUrl)) {
	Write-Host "Tool url is not defined"
	exit
}

if ([string]::IsNullOrEmpty($toolArgs)) {
	Write-Host "Arguments for the tool is not defined"
	exit
}

$toolPath = "C:\Tools\WebJobs.Script.Performance.App"
$binPath = "$toolPath\.store\webjobs.script.performance.app\1.0.0\webjobs.script.performance.app\1.0.0\tools\netcoreapp2.1\any\";

$tempFolderName = [System.IO.Path]::GetFileNameWithoutExtension([System.IO.Path]::GetTempFileName())
$tempFolder = [System.IO.Path]::Combine([System.IO.Path]::GetTempPath() ,$tempFolderName)
[System.IO.Directory]::CreateDirectory($tempFolder)
$filename = $toolNupkgUrl.Substring($toolNupkgUrl.LastIndexOf("/") + 1)
$nupkgPath = "$tempFolder\$filename"
   
Write-Host "Downloading '$toolNupkgUrl' to '$nupkgPath'"
Invoke-WebRequest -Uri $toolNupkgUrl -OutFile $nupkgPath

& dotnet tool update "WebJobs.Script.Performance.App" --add-source "$tempFolder" --tool-path $toolPath

# copy settings
Copy-Item -Path "$toolPath\local.settings.json" -Destination $binPath -Force

Push-Location "$binPath\Artifacts\PS"
Invoke-Expression "$binPath\Artifacts\PS\build-jar.ps1"
Pop-Location

Push-Location $binPath
& $toolPath\WebJobs.Script.Performance.App.exe $toolArgs
Pop-Location