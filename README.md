# DotNetDevOps.LetsEncrypt

Function App that helps generating certificates using letsencrypt

local.settings.json
```
{
  "IsEncrypted": false,
  "Values": {
    "ASPNETCORE_ENVIRONMENT": "Development",
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet",
    "DotNetDevOpsLetsEncryptHubName": "LetsEncrypHubName"
  }
}
```