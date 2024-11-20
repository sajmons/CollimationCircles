using Standard.Licensing;
using System;

namespace CollimationCircles.Services
{
    public interface ILicenseService
    {
        public bool IsLicensed { get; }
        public bool IsFeatureLicensed(string feature);
        public bool IsFeatureCount(string feature, int count);
        public string ValidationError { get; }
        public bool HasErrors { get; }
        public bool IsExpired { get; }
        public DateTime? Expiration { get; }
    }
}
