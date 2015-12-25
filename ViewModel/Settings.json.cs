using Starcounter;

namespace GitHubImporter {
    partial class Settings : Partial {
    }

    [Settings_json.Token]
    partial class SettingsTokenPartial : Json {
        protected override void HasChanged(Starcounter.Templates.TValue property) {
            Transaction.Commit();
            base.HasChanged(property);
        }
    }
}
