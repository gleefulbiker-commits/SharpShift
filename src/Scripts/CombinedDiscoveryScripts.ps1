param(
    [Parameter(Mandatory=$true)]
    [string]$Organization,
 
    [Parameter(Mandatory=$false)]
    [string]$TenantId = "e566f13d-679f-4b76-bd7f-af7cf481f8c0"
)
 
# Check if MSAL.PS module is installed
if (-not (Get-Module -ListAvailable -Name MSAL.PS)) {
    Write-Host "Installing MSAL.PS module for authentication..." -ForegroundColor Yellow
    Install-Module -Name MSAL.PS -Force -Scope CurrentUser
}
 
Import-Module MSAL.PS
 
# Azure DevOps Application ID (public client)
$clientId = "499b84ac-1321-427f-aa17-267ca6975798"
$scope = "499b84ac-1321-427f-aa17-267ca6975798/.default"
 
try {
    Write-Host "Authenticating with Azure DevOps using interactive browser login..." -ForegroundColor Cyan
 
    az login
 
    # Get access token using MSAL with interactive browser authentication
    $token = Get-MsalToken -ClientId $clientId `
                          -TenantId $TenantId `
                          -Scopes $scope `
                          -Interactive
 
    $accessToken = $token.AccessToken
 
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
    if ($_.Exception.InnerException) {
        Write-Host "Inner exception: $($_.Exception.InnerException.Message)" -ForegroundColor Red
    }
    throw
}
 
Azure DevOps | Microsoft Azure
Optimize your development process with Azure DevOps Services. Plan smarter, collaborate better, and ship faster using agile tools, CI/CD, agentic AI, and more.
 
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
 
Azure DevOps | Microsoft Azure
Optimize your development process with Azure DevOps Services. Plan smarter, collaborate better, and ship faster using agile tools, CI/CD, agentic AI, and more.
 
=================================================================

FILE: Invoke-Discovery.ps1

=================================================================
 
param(

    [Parameter(Mandatory)]

    [string]$ProjectUrl,
 
    [Parameter(Mandatory)]

    [string]$PatToken

)
 
$root = Split-Path $MyInvocation.MyCommand.Path
 
. "$root\Common\Get-AdoAuthHeader.ps1"
 
$authHeader = Get-AdoAuthHeader -PatToken $PatToken
 
$result = [ordered]@{}
 
$result += & "$root\Modules\Get-RepositoryMetrics.ps1" `

    -ProjectUrl $ProjectUrl `

    -AuthHeader $authHeader
 
$result += & "$root\Modules\Get-PipelineMetrics.ps1" `

    -ProjectUrl $ProjectUrl `

    -AuthHeader $authHeader
 
$result += & "$root\Modules\Get-ServiceConnectionMetrics.ps1" `

    -ProjectUrl $ProjectUrl `

    -AuthHeader $authHeader
 
$result += & "$root\Modules\Get-AgentPoolMetrics.ps1" `

    -ProjectUrl $ProjectUrl `

    -AuthHeader $authHeader
 
$result += & "$root\Modules\Get-BoardMetrics.ps1" `

    -ProjectUrl $ProjectUrl `

    -AuthHeader $authHeader
 
$result += & "$root\Modules\Get-TestPlanMetrics.ps1" `

    -ProjectUrl $ProjectUrl `

    -AuthHeader $authHeader
 
$result += & "$root\Modules\Get-ArtifactMetrics.ps1" `

    -ProjectUrl $ProjectUrl `

    -AuthHeader $authHeader
 
$result += & "$root\Modules\Get-UserMetrics.ps1" `

    -ProjectUrl $ProjectUrl `

    -AuthHeader $authHeader
 
[PSCustomObject]$result |

    Export-Csv .\discovery-results.csv -Append -NoTypeInformation
 
[PSCustomObject]$result
 
=================================================================

FILE: Common\\Get-AdoAuthHeader.ps1

=================================================================
 
function Get-AdoAuthHeader {
 
    param(

        [string]$PatToken

    )
 
    $base64 = :ToBase64String(

        [Text.Encoding]::ASCII.GetBytes(":$PatToken")

    )
 
    @{

        Authorization = "Basic $base64"

    }

}
 
=================================================================

FILE: Modules\\Get-RepositoryMetrics.ps1

=================================================================
 
param(

    $ProjectUrl,

    $AuthHeader

)
 
$reposUrl =

    "$ProjectUrl/_apis/git/repositories?api-version=7.1"
 
$repos =

    Invoke-RestMethod `

        -Uri $reposUrl `

        -Headers $AuthHeader
 
$repoCount = $repos.count

$branchCount = 0

$repoSize = 0
 
foreach ($repo in $repos.value)

{

    if ($repo.size)

    {

        $repoSize += $repo.size

    }
 
    $refsUrl =

        "$ProjectUrl/_apis/git/repositories/$($repo.id)/refs?filter=heads/&api-version=7.1"
 
    $refs =

        Invoke-RestMethod `

            -Uri $refsUrl `

            -Headers $AuthHeader
 
    $branchCount += $refs.count

}
 
@{

    ProjectName     = $repos.value[0].project.name

    RepositoryCount = $repoCount

    RepositorySizeMB = :Round($repoSize / 1MB, 2)

    BranchCount     = $branchCount

}
 
=================================================================

FILE: Modules\\Get-PipelineMetrics.ps1

=================================================================
 
param(

    $ProjectUrl,

    $AuthHeader

)
 
$url =

    "$ProjectUrl/_apis/pipelines?api-version=7.1"
 
$pipelines =

    Invoke-RestMethod `

        -Uri $url `

        -Headers $AuthHeader
 
@{

    PipelineCount = $pipelines.count

}
 
=================================================================

FILE: Modules\\Get-ServiceConnectionMetrics.ps1

=================================================================
 
param(

    $ProjectUrl,

    $AuthHeader

)
 
$endpointUrl =

    "$ProjectUrl/_apis/serviceendpoint/endpoints?api-version=7.1"
 
$variableGroupUrl =

    "$ProjectUrl/_apis/distributedtask/variablegroups?api-version=7.1-preview.2"
 
$endpoints =

    Invoke-RestMethod `

        -Uri $endpointUrl `

        -Headers $AuthHeader
 
$variableGroups =

    Invoke-RestMethod `

        -Uri $variableGroupUrl `

        -Headers $AuthHeader
 
@{

    ServiceConnections = $endpoints.count

    VariableGroups     = $variableGroups.count

}
 
=================================================================

FILE: Modules\\Get-AgentPoolMetrics.ps1

=================================================================
 
param(

    $ProjectUrl,

    $AuthHeader

)
 
$orgUrl =

    ($ProjectUrl -replace '/[^/]+$','')
 
$url =

    "$orgUrl/_apis/distributedtask/pools?api-version=7.1"
 
$pools =

    Invoke-RestMethod `

        -Uri $url `

        -Headers $AuthHeader
 
@{

    AgentPools = $pools.count

}
 
=================================================================

FILE: Modules\\Get-BoardMetrics.ps1

=================================================================
 
param(

    $ProjectUrl,

    $AuthHeader

)
 
$wiqlBody = @{

    query = "

        SELECT [System.Id]

        FROM WorkItems

        WHERE [System.State] <> 'Closed'

    "

} | ConvertTo-Json
 
$wiqlUrl =

    "$ProjectUrl/_apis/wit/wiql?api-version=7.1"
 
$workItems =

    Invoke-RestMethod `

        -Method Post `

        -Uri $wiqlUrl `

        -Headers $AuthHeader `

        -ContentType "application/json" `

        -Body $wiqlBody
 
@{

    BoardsUsage = $workItems.workItems.Count

}
 
=================================================================

FILE: Modules\\Get-TestPlanMetrics.ps1

=================================================================
 
param(

    $ProjectUrl,

    $AuthHeader

)
 
$url =

    "$ProjectUrl/_apis/testplan/plans?api-version=7.1-preview.1"
 
$plans =

    Invoke-RestMethod `

        -Uri $url `

        -Headers $AuthHeader
 
@{

    TestPlansUsage = $plans.count

}
 
=================================================================

FILE: Modules\\Get-ArtifactMetrics.ps1

=================================================================
 
param(

    $ProjectUrl,

    $AuthHeader

)
 
try

{

    $feedsUrl =

        "$ProjectUrl/_apis/packaging/feeds?api-version=7.1-preview.1"
 
    $feeds =

        Invoke-RestMethod `

            -Uri $feedsUrl `

            -Headers $AuthHeader
 
    @{

        ArtifactsUsage = $feeds.count

    }

}

catch

{

    @{

        ArtifactsUsage = 0

    }

}
 
=================================================================

FILE: Modules\\Get-UserMetrics.ps1

=================================================================
 
param(

    $ProjectUrl,

    $AuthHeader

)
 
$orgUrl =

    ($ProjectUrl -replace '/[^/]+$','')
 
$url =

    "$orgUrl/_apis/graph/users?api-version=7.1-preview.1"
 
$users =

    Invoke-RestMethod `

        -Uri $url `

        -Headers $AuthHeader
 
@{

    UserCount = $users.count

}
 
=================================================================

SAMPLE OUTPUT CSV

=================================================================
 
ProjectName,

RepositoryCount,

RepositorySizeMB,

BranchCount,

PipelineCount,

ServiceConnections,

VariableGroups,

AgentPools,

BoardsUsage,

TestPlansUsage,

ArtifactsUsage,

UserCount
 
FinanceSystems,

24,

589.43,

112,

18,

7,

11,

3,

1456,

32,

4,

87
 