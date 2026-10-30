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

        //Project analysis messages
        [LoggerMessage(EventId = 2001, Level = LogLevel.Information, Message = "Analyzing project '{ProjectPath}'")]
        public static partial void AnalyzingProject(ILogger logger, string projectPath);

        [LoggerMessage(EventId = 2002, Level = LogLevel.Warning, Message = "Project path '{ProjectPath}' is invalid.")]
        public static partial void ProjectPathIsInvalid(ILogger logger, string projectPath);

        [LoggerMessage(EventId = 2003, Level = LogLevel.Warning, Message = "No dependencies found for project '{ProjectPath}'.")]
        public static partial void NoDependenciesFound(ILogger logger, string projectPath);

        //Report generation messages
        [LoggerMessage(EventId = 4000, Level = LogLevel.Information, Message = "Generating report for '{InputDirectory}' with output '{OutputDirectory}'")]
        public static partial void GeneratingReport(ILogger logger, string inputDirectory, string outputDirectory);
    }
}
