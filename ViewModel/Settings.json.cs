using Starcounter;
using System;

namespace GitHubImporter {
    partial class Settings : Partial {
    }

    [Settings_json.Token]
    partial class SettingsTokenPartial : Json {
        protected override void HasChanged(Starcounter.Templates.TValue property) {
            var token = Data as SettingsToken;
            token.Limit = 0;
            token.Remaining = 0;
            token.ResetAt = DateTime.UtcNow;
            Transaction.Commit();
            base.HasChanged(property);
        }
    }
}
