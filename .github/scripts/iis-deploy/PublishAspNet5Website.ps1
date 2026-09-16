[CmdletBinding()]

param(
	[Parameter(Mandatory=$true)]
	[ValidateScript( {Test-Path -Path $PSItem -IsValid})]
	[string]$packOutput, 
	
	[Parameter(Mandatory=$true)]
	[string]$deployUrl,

	[Parameter(Mandatory=$true)]
	[string]$websiteName,

	[Parameter(Mandatory=$true)]
	[string]$deployUserName,
	
	[Parameter(Mandatory=$true)]
	[string]$deployUserPassword,

	[Parameter(Mandatory=$false)]
	$skipExtraFilesOnServer = $false
)

Write-Host "Deployment starting..."

# explicitly convert the parameter to boolean if received as a string (GitHub Actions/YAML case)
if ($skipExtraFilesOnServer -is [string]) {
	$skipExtraFilesOnServer = [System.Convert]::ToBoolean($skipExtraFilesOnServer)
}

$publishProperties = @{'WebPublishMethod'='MSDeploy';
						'MSDeployServiceUrl'=$deployUrl;
						'DeployIisAppPath'=$websiteName;
						'Username'=$deployUserName;
						'Password'=$deployUserPassword;
						'SkipExtraFilesOnServer'=$skipExtraFilesOnServer;
						'EnableMSDeployAppOffline'=$true;
						# Preserve runtime state across clean deploys (skip-extra-files=false):
						# - logs: stdout / AspNetCore debug logs
						# - data: optional host data
						# - App_Data: DataProtection keys (cookies, antiforgery, Identity email tokens)
						'ExcludeFiles'=@(
							@{'objectname'='filePath';'absolutepath'='.*google.*\.html'},
							@{'objectname'='filePath';'absolutepath'='.*BingSiteAuth\.xml'},
							@{'objectname'='filePath';'absolutepath'='logs\\.*'},
							@{'objectname'='dirPath';'absolutepath'='logs'},
							@{'objectname'='filePath';'absolutepath'='data\\.*'},
							@{'objectname'='dirPath';'absolutepath'='data'},
							@{'objectname'='filePath';'absolutepath'='App_Data\\.*'},
							@{'objectname'='dirPath';'absolutepath'='App_Data'}
						)}


$publishScript = Join-Path (Split-Path $MyInvocation.MyCommand.Path) 'default-publish.ps1'

. $publishScript -publishProperties $publishProperties -packOutput $packOutput

Write-Host "Deployment ending"
