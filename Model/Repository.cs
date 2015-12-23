using System;
using Starcounter;

namespace GitHubImporter {
    [Database]
    public class Repository {
        public User Owner;
        public string Name;
    }
}
