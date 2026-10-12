param(
    [Parameter(Mandatory=$true)]
    [string]$Organization,
 
    [Parameter(Mandatory=$false)]
    [string]$TenantId = "e566f13d-679f-4b76-bd7f-af7cf481f8c0"
)
 
# Azure DevOps resource scope
$scope = "499b84ac-1321-427f-aa17-267ca6975798/.default"
 
try {
    Write-Host "Authenticating with Azure DevOps..." -ForegroundColor Cyan
 
    az login
    # Get access token using Azure CLI (requires 'az login' to be run first)
    # Alternative: You can use Azure PowerShell modules or MSAL.PS
    $tokenResult = az account get-access-token --resource 499b84ac-1321-427f-aa17-267ca6975798 --query accessToken -o tsv
 
    if (-not $tokenResult) {
        throw "Failed to get access token. Please run 'az login' first."
    }
 
    $accessToken = $tokenResult
 
    # Set up the API call
    $uri = "https://dev.azure.com/$Organization/_apis/projects"
 
    # Create headers
    $headers = @{
        'Authorization' = "Bearer $accessToken"
        'Content-Type' = 'application/json'
        'Accept' = 'application/json'
    }
 
    Write-Host "Fetching projects from Azure DevOps..." -ForegroundColor Cyan
 
    # Make the API call
    $response = Invoke-RestMethod -Uri $uri -Method Get -Headers $headers
 
    # Display results
    Write-Host "`nSuccessfully retrieved projects!" -ForegroundColor Green
    Write-Host "Organization: $Organization" -ForegroundColor Yellow
    Write-Host "Total projects: $($response.count)" -ForegroundColor Yellow
    Write-Host "`nProjects:" -ForegroundColor Yellow
    Write-Host ("=" * 80) -ForegroundColor Gray
 
    foreach ($project in $response.value) {
        Write-Host "`nName: $($project.name)" -ForegroundColor White
        Write-Host "  ID: $($project.id)" -ForegroundColor Gray
        Write-Host "  Description: $($project.description)" -ForegroundColor Gray
        Write-Host "  State: $($project.state)" -ForegroundColor Gray
        Write-Host "  Visibility: $($project.visibility)" -ForegroundColor Gray
        Write-Host "  Last Update: $($project.lastUpdateTime)" -ForegroundColor Gray
    }
 
    # Return the raw response for further processing if needed
    return $response
}
catch {
    Write-Host "`nError occurred: $($_.Exception.Message)" -ForegroundColor Red
    throw
}
 