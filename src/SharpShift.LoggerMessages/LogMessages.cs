using Microsoft.Extensions.Logging;

namespace SharpShift.LoggerMessages
{
    public static partial class LogMessages
    {
        // Solution scanning messages
        [LoggerMessage(EventId = 1000, Level = LogLevel.Information, Message = "Starting solution scan for '{Path}' with output '{OutputFile}'")]
        public static partial void StartingSolutionScan(ILogger logger, string path, string outputFile);

        // Solution discovery messages
        [LoggerMessage(EventId = 2000, Level = LogLevel.Information, Message = "Evaluating .slnx solution properties for {SolutionPath}")]
        public static partial void EvaluatingSolutionProperties(ILogger logger, string solutionPath);

        [LoggerMessage(EventId = 2001, Level = LogLevel.Information, Message = "Evaluating local solution file: {SolutionPath}")]
        public static partial void EvaluatingLocalSolutionFile(ILogger logger, string solutionPath);

        [LoggerMessage(EventId = 2005, Level = LogLevel.Information, Message = "Discovered {SolutionCount} local solution files under {RootPath}")]
        public static partial void DiscoveredLocalSolutionFiles(ILogger logger, int solutionCount, string rootPath);

        [LoggerMessage(EventId = 2006, Level = LogLevel.Information, Message = "Discovered Git URL: {url}")]
        public static partial void DiscoveredGitUrl(ILogger logger, string url);

        [LoggerMessage(EventId = 2007, Level = LogLevel.Information, Message = "Git URLs file found: {gitUrlsPath}")]
        public static partial void GitUrlsFileFound(ILogger logger, string gitUrlsPath);

        [LoggerMessage(EventId = 2008, Level = LogLevel.Warning, Message = "Git URLs file does not exist: {gitUrlsPath}")]
        public static partial void GitUrlsFileNotFound(ILogger logger, string gitUrlsPath);

        // Handle failed clone
        [LoggerMessage(EventId = 2009, Level = LogLevel.Error, Message = "Failed to clone repository {RepoUrl} into {TargetDirectory}")]
        public static partial void FailedToCloneRepository(ILogger logger, string repoUrl, string targetDirectory);

        // Writing inventory messages
        [LoggerMessage(EventId = 3000, Level = LogLevel.Information, Message = "Writing inventory to '{OutputFile}'")]
        public static partial void WritingInventory(ILogger logger, string outputFile);

        [LoggerMessage(EventId = 3001, Level = LogLevel.Information, Message = "Writing analysis results to '{OutputFile}'")]
        public static partial void WritingAnalysisResults(ILogger logger, string outputFile);

        // Solution analysis messages
        [LoggerMessage(EventId = 1001, Level = LogLevel.Information, Message = "Starting analysis for '{Path}' with output '{OutputFile}'")]
        public static partial void StartingAnalysis(ILogger logger, string path, string outputFile);

        public const string StartingSolutionAnalysis = "Starting solution analysis.";
        public const string StartingProjectAnalysis = "Starting project analysis.";
        public const string StartingDependencyAnalysis = "Starting dependency analysis.";
        public const string StartingRiskAnalysis = "Starting risk analysis.";

        //Remote repository messages
        [LoggerMessage(EventId = 5000, Level = LogLevel.Information, Message = "Discovered remote GitHub repository: {RepoUrl} (archived: {IsArchived})")]
        public static partial void DiscoveredRemoteGitHubRepository(ILogger logger, string repoUrl, bool isArchived);   

        [LoggerMessage(EventId = 5001, Level = LogLevel.Information, Message = "Discovering repositories for GitHub account {Account}")]
        public static partial void DiscoveringRepositoriesForGitHubAccount(ILogger logger, string account);

        [LoggerMessage(EventId = 5002, Level = LogLevel.Information, Message = "GitHub discovery completed with {EntryCount} repository entries")]
        public static partial void GitHubDiscoveryCompleted(ILogger logger, int entryCount);

        [LoggerMessage(EventId = 5003, Level = LogLevel.Information, Message = "Starting solution discovery for root path {RootPath}")]
        public static partial void StartingSolutionDiscovery(ILogger logger, string rootPath);

        [LoggerMessage(EventId = 5004, Level = LogLevel.Information, Message = "Starting clone for repository {RepoUrl} into {TargetDirectory}")]
        public static partial void StartingRepositoryClone(ILogger logger, string repoUrl, string targetDirectory);

        [LoggerMessage(EventId = 5005, Level = LogLevel.Information, Message = "Skipping clone for {RepoUrl} because target directory already exists: {TargetDirectory}")]
        public static partial void SkippingCloneTargetExists(ILogger logger, string repoUrl, string targetDirectory);

        [LoggerMessage(EventId = 5006, Level = LogLevel.Information, Message = "Skipping clone for archived repository {RepoUrl}")]
        public static partial void SkippingArchivedRepositoryClone(ILogger logger, string repoUrl);

        [LoggerMessage(EventId = 5007, Level = LogLevel.Information, Message = "Clone resolved for {RepoUrl}. Local path: {LocalPath}")]
        public static partial void RepositoryCloneResolved(ILogger logger, string repoUrl, string localPath);

        [LoggerMessage(EventId = 5008, Level = LogLevel.Information, Message = "Discovered {SolutionCount} solution files under cloned repository root {CloneRoot}")]
        public static partial void DiscoveredClonedRepositorySolutions(ILogger logger, string cloneRoot, int solutionCount);

        [LoggerMessage(EventId = 5009, Level = LogLevel.Debug, Message = "Skipping duplicate solution evaluation for {SolutionPath}")]
        public static partial void SkippingDuplicateSolutionEvaluation(ILogger logger, string solutionPath);

        [LoggerMessage(EventId = 5010, Level = LogLevel.Information, Message = "Starting git clone operation.")]
        public static partial void StartingGitCloneOperation(ILogger logger);

        [LoggerMessage(EventId = 5011, Level = LogLevel.Information, Message = "Repository URL: {RepoUrl}")]
        public static partial void GitCloneRepositoryUrl(ILogger logger, string repoUrl);

        [LoggerMessage(EventId = 5012, Level = LogLevel.Information, Message = "Target Directory: {TargetDir}")]
        public static partial void GitCloneTargetDirectory(ILogger logger, string targetDir);

        [LoggerMessage(EventId = 5013, Level = LogLevel.Information, Message = "Clone Depth: {Depth}")]
        public static partial void GitCloneDepth(ILogger logger, int depth);

        [LoggerMessage(EventId = 5014, Level = LogLevel.Information, Message = "Timeout (ms): {TimeoutMs}")]
        public static partial void GitCloneTimeout(ILogger logger, int timeoutMs);

        [LoggerMessage(EventId = 5016, Level = LogLevel.Information, Message = "Git discovery completed. Discovered {SolutionCount} solutions.")]
        public static partial void GitDiscoveryCompleted(ILogger logger, int solutionCount);

        [LoggerMessage(EventId = 5017, Level = LogLevel.Information, Message = "File system discovery completed. Discovered {SolutionCount} solutions.")]
        public static partial void FileSystemDiscoveryCompleted(ILogger logger, int solutionCount);

        //Project analysis messages
        [LoggerMessage(EventId = 2002, Level = LogLevel.Warning, Message = "Project path '{ProjectPath}' is invalid.")]
        public static partial void ProjectPathIsInvalid(ILogger logger, string projectPath);

        [LoggerMessage(EventId = 2003, Level = LogLevel.Warning, Message = "No dependencies found for project '{ProjectPath}'.")]
        public static partial void NoDependenciesFound(ILogger logger, string projectPath);

        [LoggerMessage(EventId = 2004, Level = LogLevel.Information, Message = "Analyzing project '{ProjectPath}'")]
        public static partial void AnalyzingProject(ILogger logger, string projectPath);

        //Dependency analysis messages
        [LoggerMessage(EventId = 3002, Level = LogLevel.Information, Message = "Analyzing DEPRECATED dependency '{DependencyName}' with version '{Version}'")]
        public static partial void AnalyzingDependency(ILogger logger, string dependencyName, string version);

        //Risk analysis messages
        [LoggerMessage(EventId = 4001, Level = LogLevel.Information, Message = "Analyzing risk for dependency '{DependencyName}' with version '{Version}'")]
        public static partial void AnalyzingRisk(ILogger logger, string dependencyName, string version);

        [LoggerMessage(EventId = 4002, Level = LogLevel.Critical, Message = "Failed to add version '{VersionKey}' for dependency '{DependencyName}'")]
        public static partial void FailedToAddVersion(ILogger logger, string versionKey, string dependencyName);

        //Report generation messages
        [LoggerMessage(EventId = 4000, Level = LogLevel.Information, Message = "Generating report for '{InputDirectory}' with output '{OutputDirectory}'")]
        public static partial void GeneratingReport(ILogger logger, string inputDirectory, string outputDirectory);
    }
}
