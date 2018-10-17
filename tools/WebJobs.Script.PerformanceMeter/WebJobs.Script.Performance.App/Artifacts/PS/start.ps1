param (
    [string]$extensionUrl = "test",
	[string]$performaceMeterUrl = "https://functionsperfst.blob.core.windows.net/test/WebJobs.Script.PerformanceMeter.1.0.0.nupkg",
    [string]$commandName = "",
    [string]$commandValue = ""
)

$toolPath = "C:\Tools\WebJobs.Script.Performance.App"


if ([string]::IsNullOrEmpty($extensionUrl)) {
	Write-Host "Extension url is not defined"
	exit
}

Invoke-Expression .\build-jar.ps1

$tempFolderName = [System.IO.Path]::GetFileNameWithoutExtension([System.IO.Path]::GetTempFileName())
$tempFolder = [System.IO.Path]::Combine([System.IO.Path]::GetTempPath() ,$tempFolderName)
[System.IO.Directory]::CreateDirectory($tempFolder)
$filename = $nupkgUrl.Substring($nupkgUrl.LastIndexOf("/") + 1)
$nupkgPath = "$tempFolder\$filename"
    

Write-Host "Downloading '$nupkgUrl' to '$nupkgPath'"
Invoke-WebRequest -Uri $nupkgUrl -OutFile $nupkgPath

& dotnet tool update "WebJobs.Script.Performance.App" --add-source "$tempFolder" --tool-path $toolPath

& $toolPath\WebJobs.Script.PerformanceMeter.exe '$commandName' '$commandValue'

#C:\git\V2-azure-webjobs-sdk-script\tools\WebJobs.Script.PerformanceMeter\WebJobs.Script.PerformanceMeter>dotnet tool install WebJobs.Script.PerformanceMeter --tool-path "C:\Tools\WebJobs.Script.PerformanceMeter" --add-source "C:\git\V2-azure-webjobs-sdk-script\tools\WebJobs.Script.PerformanceMeter\WebJobs.Script.PerformanceMeter\nupkg"