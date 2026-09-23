Param(
	[string]$Message = "feat(cli): initial inventory scan scaffold + sample repo test"
)

Write-Host "Staging changes..."
git add -A

Write-Host "Committing: $Message"
git commit -m $Message

Write-Host "Pushing to origin..."
git push origin HEAD

Write-Host "Done."
