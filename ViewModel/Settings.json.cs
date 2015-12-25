using Starcounter;
using System;

namespace GitHubImporter {
    partial class Settings : Partial {
    }

    [Settings_json.Token]
    partial class SettingsTokenPartial : Json, IBound<SettingsToken> {
        void Handle(Input.Token action) {
            Data.Token = action.Value;
            Data.Limit = 0;
            Data.Remaining = 0;
            Data.ResetAt = DateTime.UtcNow;
            Transaction.Commit();
        }
    }
}
