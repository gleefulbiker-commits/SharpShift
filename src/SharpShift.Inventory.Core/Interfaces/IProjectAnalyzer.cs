using System.Threading.Tasks;
using SharpShift.Inventory.Core.Models;

namespace SharpShift.Inventory.Core.Interfaces
{
    /// <summary>
    /// Analyzes a project context and returns detection results.
    /// </summary>
    public interface IProjectAnalyzer
    {
        /// <summary>
        /// Analyze the given project and return a <see cref="ProjectAnalysisResult"/>.
        /// Implementations should be lightweight and focused on a single concern.
        /// </summary>
        Task<ProjectAnalysisResult> AnalyzeAsync(ProjectContext context);
    }
}
