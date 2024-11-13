using CollimationCircles.ViewModels;
using CollimationCirclesFeatures;
using CommunityToolkit.Mvvm.DependencyInjection;
using HanumanInstitute.MvvmDialogs;
using HanumanInstitute.MvvmDialogs.Avalonia;
using HanumanInstitute.MvvmDialogs.FrameworkDialogs;
using Standard.Licensing;
using Standard.Licensing.Validation;
using System;
using System.IO;
using System.Linq;

namespace CollimationCircles.Services
{
    internal class LicenseService : ILicenseService
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private readonly string publicKey = "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAE1dtu67ItK9HISDiRoVhsFiZSYHx6jd4WIt7D7eID6t5Fmp4NNs15OsIVdoP9elH4r8xcHR0qryAh54mPm2bdjQ==";

        private readonly License? license;

        public bool IsLicensed => license != null;

        public LicenseService()
        {
            license = LoadLicense();
        }

        private License? LoadLicense()
        {
            try
            {
                logger.Info($"Loading licence file");

                using TextReader textReader = new StreamReader("./license.lic");

                License license = License.Load(textReader);

                textReader.Close();

                string clientId = libc.hwid.HwId.Generate();

                var validationFailures = license.Validate()
                                .ExpirationDate(DateTime.Now)
                                .And()
                                .Signature(publicKey)
                                .And()
                                .AssertThat(lic => lic.AdditionalAttributes.Get("ClientId") == clientId,
                                    new GeneralValidationFailure { Message = "ClientId mismatch" })
                                .AssertValidLicense();

                if (validationFailures.Any())
                {
                    foreach (var failure in validationFailures)
                    {
                        logger.Error(failure.GetType().Name + ": " + failure.Message + " - " + failure.HowToResolve);
                    }
                }
                else
                {
                    logger.Info($"License: {license?.Type}, {license?.Customer.Name}, {license?.Customer.Email}, {license?.Id}");
                    return license;
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.Message);
            }

            return null;
        }

        public bool IsFeatureLicensed(string feature)
        {
            if (IsLicensed)
            {
                var validationFailures = license.Validate()
                    .ExpirationDate(DateTime.Now)
                    .And()
                    .AssertThat(lic => lic.ProductFeatures.Get(feature) == "true",
                        new GeneralValidationFailure { Message = $"Feature '{feature}' not licensed" })
                    .AssertValidLicense();

                if (validationFailures.Any())
                {
                    foreach (var failure in validationFailures)
                    {
                        logger.Error(failure.GetType().Name + ": " + failure.Message + " - " + failure.HowToResolve);
                    }
                }
                else
                {
                    logger.Info($"Feature '{feature}' is licensed");
                    return true;
                }
            }

            return false;
        }

        public bool IsFeatureCount(string feature, int count)
        {
            try
            {
                if (IsLicensed)
                {
                    int maxCount = 0;

                    var validationFailures = license.Validate()
                        .ExpirationDate(DateTime.Now)
                        .And()
                        .AssertThat(lic =>
                        {
                            maxCount = int.Parse(lic.ProductFeatures.Get(feature));
                            return count < maxCount;
                        },
                        new GeneralValidationFailure { Message = $"Count '{count}' exceed max. feature '{feature}' count of '{maxCount + 1}'." })
                        .AssertValidLicense();

                    if (validationFailures.Any())
                    {
                        foreach (var failure in validationFailures)
                        {
                            logger.Error(failure.GetType().Name + ": " + failure.Message + " - " + failure.HowToResolve);
                        }
                    }
                    else
                    {
                        logger.Info($"Feature '{feature}' count of '{count}' is licensed");
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.Message);

            }

            return false;
        }

        public override string ToString()
        {
            return $"{license?.Type}, {license?.Customer.Name}, {license?.Customer.Email}, {license?.Id}";
        }
    }
}
