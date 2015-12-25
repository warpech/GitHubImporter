using Starcounter;
using System;

namespace GitHubImporter {
    [Database]
    public class SettingsToken {
        public string Token;
        public Int64 Remaining;
        public Int64 Limit;
        public DateTime ResetAt;
    }
}
