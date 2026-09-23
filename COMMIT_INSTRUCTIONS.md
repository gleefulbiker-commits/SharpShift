Run the included script to stage, commit, and push changes from the repository root:

PowerShell (Windows):

	.\scripts\commit_and_push.ps1 -Message "feat(cli): snapshot commit"

Or do it manually:

	git add -A
	git commit -m "feat(cli): snapshot commit"
	git push origin HEAD

Note: this environment cannot run git commands for you — run the script locally in your dev environment.