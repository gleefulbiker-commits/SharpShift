using SharpShift.Inventory.Core.Interfaces;
using SharpShift.Inventory.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace SharpShift.Inventory.Discovery
{
    public class TfvcSolutionDiscoverer : ISolutionDiscoverer
    {
        public Task<IEnumerable<SolutionDiscoveryEntry>> DiscoverSolutionsAsync(string? rootPath = null, string? gitUrlsPath = null)
        {
            throw new NotImplementedException();
        }
    }
}
