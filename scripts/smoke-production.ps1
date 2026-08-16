param(
  [Parameter(Mandatory=$true)][string]$BaseUrl,
  [string]$SiteSlug = ""
)
$ErrorActionPreference = "Stop"
$BaseUrl = $BaseUrl.TrimEnd('/')
function Test-Endpoint([string]$Path) {
  Write-Host "[smoke] GET $Path"
  $response = Invoke-WebRequest -Uri "$BaseUrl$Path" -MaximumRedirection 5 -TimeoutSec 15 -UseBasicParsing
  if ($response.StatusCode -lt 200 -or $response.StatusCode -ge 400) { throw "Smoke failed: $Path -> $($response.StatusCode)" }
}
@('/health/live','/health/ready','/','/rooms','/blog','/sitemap.xml','/robots.txt') | ForEach-Object { Test-Endpoint $_ }
if ($SiteSlug) { @("/h/$SiteSlug","/h/$SiteSlug/rooms","/h/$SiteSlug/blog") | ForEach-Object { Test-Endpoint $_ } }
Write-Host '[smoke] PASS'
