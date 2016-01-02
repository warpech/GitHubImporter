using Starcounter;
using Starcounter.Internal;
using System;

namespace GitHubImporter {
    partial class Settings : Partial {
    }

    [Settings_json.Token]
    partial class SettingsTokenPartial : Json, IBound<SettingsToken> {
        public GitHubApiHelper ghHelper;

        void Handle(Input.Token action) {
            Data.Token = action.Value;
            Data.Limit = 0;
            Data.Remaining = 0;
            Data.ResetAt = DateTime.UtcNow;
            Transaction.Commit();
            ghHelper.CreateClient();
            RefreshLimits();
        }

        public void RefreshLimits() {
            if (ghHelper.Client != null) {
                IsLoading = true;

                byte schedulerId = StarcounterEnvironment.CurrentSchedulerId;
                string appName = StarcounterEnvironment.AppName;
                var session = Session.Current;
                var waiting = ghHelper.GetRateLimit();
                waiting.ContinueWith(a => {
                    new DbSession().RunSync(() => {
                        StarcounterEnvironment.RunWithinApplication(appName, () => {
                            if (waiting.Result != null) {
                                Db.Transact(() => {
                                    Data.Remaining = waiting.Result.Resources.Core.Remaining;
                                    Data.Limit = waiting.Result.Resources.Core.Limit;
                                    Data.ResetAt = waiting.Result.Resources.Core.Reset.UtcDateTime;
                                });
                            }
                            IsLoading = false;
                            session.CalculatePatchAndPushOnWebSocket();
                        });
                    }, schedulerId);
                });
            }
        }
    }
}
