using System;
using System.Collections.Generic;
using System.Text;

namespace SharpShift.Inventory.Core.Models
{
    public static class RepoSourceType
    {
        public const string FileSystem = "FileSystem";
        public const string GitHub = "GitHub";
        public const string OtherGit = "Git";
        public const string Tfvc = "Tfvc";
    }
}
