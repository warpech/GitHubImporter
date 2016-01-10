using System;
using System.Collections.Generic;
using System.Linq;

namespace GitHubImporter {

    public class QueryBuilder {
        public string where = "";
        public string suffix = "";
        public List<dynamic> whereParams = new List<dynamic>();
        public List<dynamic> suffixParams = new List<dynamic>();

        public void Where(string addition, dynamic additionParam) {
            if (where != "") {
                where += "AND ";
            }
            where += addition + " ";
            whereParams.Add(additionParam);
        }

        public void Fetch(int count) {
            suffix += "FETCH ? ";
            suffixParams.Add(count);
        }

        public void OrderBy(string orderBy) {
            suffix += "ORDER BY " + orderBy + " ";
        }

        public string Query {
            get {
                return where + suffix;
            }
        }

        public dynamic[] Params {
            get {
                return whereParams.Union(suffixParams).ToArray();
            }
        }
    }
}
