using System;
using Starcounter;

namespace GitHubImporter {
    [Database]
    public class Label {
        public string Name;
        public string Color;
        public string Url;
    }
}
