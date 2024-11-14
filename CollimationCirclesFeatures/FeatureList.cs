namespace CollimationCirclesFeatures
{
    public class FeatureList : List<Feature>
    {
        public const string ScopeManager = "Scope Manager";        

        public FeatureList()
        {
            AddRange([                
                new Feature { Name = ScopeManager }
            ]);
        }

        public Dictionary<string, string> ToDictionary(bool isTrial)
        {
            Dictionary<string, string> keyValuePairs = [];

            foreach (Feature f in this)
            {
                if (f.Numeric == null)
                {
                    keyValuePairs.Add(f.Name, isTrial ? "true" : $"{f.Enabled}");
                }
                else
                {
                    keyValuePairs.Add(f.Name, $"{f.Numeric}");
                }
            }

            return keyValuePairs;
        }
    }
}
