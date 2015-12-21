using System;
using Starcounter;

namespace GitHubImporter {
    [Database]
    public class User {
        public string Name;
        public string Url;
        public string AvatarUrl;
    }
}
