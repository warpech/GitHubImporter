using Starcounter;

namespace GitHubImporter {
    partial class Master : Partial {
        private SettingsToken Token = Db.SQL<SettingsToken>("SELECT s FROM SettingsToken s FETCH ?", 1).First;

        public bool TokenWarningVisible {
            get {
                return (Token.Token == null || Token.Token == "");
            }
        }
    }
}
