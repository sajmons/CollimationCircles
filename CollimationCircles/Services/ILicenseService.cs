using Standard.Licensing;

namespace CollimationCircles.Services
{
    public interface ILicenseService
    {
        public bool IsLicensed { get; }
        public bool IsFeatureLicensed(string feature);
        public bool IsFeatureCount(string feature, int count);
    }
}
