namespace CollimationCirclesFeatures
{
    public class FeatureList : List<Feature>
    {
        public const string CameraVideoStream = "Camera Video Stream";
        public const string ScopeManager = "Scope Manager";
        public const string Themes = "Themes";
        public const string BahtinovMask = "Bahtinov Mask";
        public const string Screw = "Screw";
        public const string PrimaryClip = "PrimaryClip";
        public const string SaveShapeList = "Save Shape List";
        public const string LoadShapeList = "Load Shape List";
        public const string ShowShapeLabels = "Show Shape Labels";
        public const string ShapeListMaxCount = "Shape List Max Count";
        public const string GlobalProperties = "Global Properties";

        public FeatureList()
        {
            AddRange([
                new Feature { Name = CameraVideoStream },
                new Feature { Name = ScopeManager },
                new Feature { Name = Themes },
                new Feature { Name = BahtinovMask },
                new Feature { Name = Screw },
                new Feature { Name = PrimaryClip },
                new Feature { Name = SaveShapeList },
                new Feature { Name = LoadShapeList },
                new Feature { Name = ShowShapeLabels },
                new Feature { Name = ShapeListMaxCount, Numeric = 3 },
                new Feature { Name = GlobalProperties },
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
                    string newVal;

                    if (isTrial && f.Name == ShapeListMaxCount)
                        newVal = "10"; // standard license
                    else
                        newVal = "3"; // trial license

                    keyValuePairs.Add(f.Name, newVal);
                }
            }

            return keyValuePairs;
        }
    }
}
