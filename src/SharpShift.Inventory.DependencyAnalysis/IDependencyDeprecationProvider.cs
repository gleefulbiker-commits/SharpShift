
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SharpShift.Inventory.DependencyAnalysis;

public interface IDependencyDeprecationProvider
{
    Task<IReadOnlyList<PackageDeprecationRecord>> GetDeprecationsAsync(ILogger logger, PackageHealthQuery query, CancellationToken cancellationToken);
}
