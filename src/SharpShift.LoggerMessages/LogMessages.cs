using Microsoft.Extensions.Logging;

namespace SharpShift.LoggerMessages
{
    public static partial class LogMessages
    {
        [LoggerMessage(EventId = 1001, Level = LogLevel.Information, Message = "Starting analysis for '{Path}' with output '{OutputFile}'")]
        public static partial void StartingAnalysis(ILogger logger, string path, string outputFile);
        public const string StartingSolutionAnalysis = "Starting solution analysis.";
        public const string StartingProjectAnalysis = "Starting project analysis.";
        public const string StartingDependencyAnalysis = "Starting dependency analysis.";
        public const string StartingRiskAnalysis = "Starting risk analysis.";
    }
}
