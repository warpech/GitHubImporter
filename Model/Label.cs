using System;
using Starcounter;

namespace GitHubImporter {
    [Database]
    public class Label {
        public Repository Repository;
        public string Name;
        public string Color;
    }
}
