param (
	[string]$nupkgUrl = "https://functionsperfst.blob.core.windows.net/test/WebJobs.Script.Performance.App.1.0.0.nupkg",
    [string]$args = "'-t' 'win-csharp-ping.jmx' '-r' 'https://ci.appveyor.com/api/buildjobs/ax3jch5m0d57hdkm/artifacts/Functions.Private.2.0.12165.win-x32.inproc.zip'"
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

& $toolPath\WebJobs.Script.Performance.App.exe $args

#C:\git\V2-azure-webjobs-sdk-script\tools\WebJobs.Script.PerformanceMeter\WebJobs.Script.PerformanceMeter>dotnet tool install WebJobs.Script.PerformanceMeter --tool-path "C:\Tools\WebJobs.Script.PerformanceMeter" --add-source "C:\git\V2-azure-webjobs-sdk-script\tools\WebJobs.Script.PerformanceMeter\WebJobs.Script.PerformanceMeter\nupkg"
#C:\git\V2-azure-webjobs-sdk-script\buildoutput>dotnet tool install WebJobs.Script.Performance.App --tool-path "C:\Tools\WebJobs.Script.Performance.App" --add-source "C:\git\V2-azure-webjobs-sdk-script\buildoutput"