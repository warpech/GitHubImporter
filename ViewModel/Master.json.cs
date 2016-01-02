using Starcounter;

namespace GitHubImporter {
    partial class Master : Partial {
        public GitHubApiHelper ghHelper;

        public string GitHubApiErrorString {
            get {
                if(ghHelper.LastError != null) {
                    return ghHelper.LastError.GetType().Name;
                }
                return "";
            }
        }
    }
}
