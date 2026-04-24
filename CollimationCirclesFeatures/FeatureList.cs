namespace CollimationCirclesFeatures
{
    public class FeatureList : List<Feature>
    {
        public const string ProfileManager = "Profile Manager";
        public const string MaxHelperItemsCount = "Max helper items count";
        public const string LiveAnalysis = "Live Analysis";

        public FeatureList()
        {
            //AddRange([
            //    new Feature { Name = ProfileManager },
            //    new Feature { Name = MaxHelperItemsCount, Numeric = 5 }
            //]);
            AddRange([
                new Feature { Name = LiveAnalysis }
            ]);
        }

        public Dictionary<string, string> ToDictionary()
        {
            Dictionary<string, string> keyValuePairs = [];

            foreach (Feature f in this)
            {
                if (f.Numeric == null)
                {
                    keyValuePairs.Add(f.Name, "true");
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
