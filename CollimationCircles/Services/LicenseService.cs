using Standard.Licensing;
using Standard.Licensing.Validation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CollimationCircles.Services
{
    internal class LicenseService : ILicenseService
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private readonly string publicKey = "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAE1dtu67ItK9HISDiRoVhsFiZSYHx6jd4WIt7D7eID6t5Fmp4NNs15OsIVdoP9elH4r8xcHR0qryAh54mPm2bdjQ==";

        private readonly License? license;

        private IEnumerable<IValidationFailure>? validationFailures;

        public bool HasLicense => license != null;

        public bool HasErrors => validationFailures?.FirstOrDefault() != null;

        public string ValidationError => validationFailures?.FirstOrDefault()?.Message ?? string.Empty;

        public bool IsExpired => license?.Expiration < DateTime.Now;

        public DateTime? Expiration => license?.Expiration;

        public LicenseService(string productName)
        {
            license = LoadLicense(productName);
        }

        private License? LoadLicense(string productName)
        {
            License? license = null;

            try
            {
                logger.Info($"Loading licence file");

                using TextReader textReader = new StreamReader("./license.lic");

                license = License.Load(textReader);

                textReader.Close();

                string clientId = libc.hwid.HwId.Generate();

                string majorProductVersion = AppService.GetAppMajorVersion();

                validationFailures = license.Validate()
                                .ExpirationDate(DateTime.Now)
                                .And()
                                .Signature(publicKey)
                                .And()
                                .AssertThat(lic => lic.AdditionalAttributes.Get("ClientId") == clientId,
                                    new GeneralValidationFailure { Message = "ClientId mismatch" })
                                .And()
                                .AssertThat(lic => lic.AdditionalAttributes.Get("Product") == productName,
                                    new GeneralValidationFailure { Message = "Product name mismatch" })
                                .And()
                                .AssertThat(lic => lic.AdditionalAttributes.Get("MajorProductVersion") == majorProductVersion,
                                    new GeneralValidationFailure { Message = "Major product version number mismatch" })
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
                    logger.Info($"License: {license?.Type}, {license?.Customer.Name}, {license?.Customer.Email}, {license?.Expiration}");
                    return license;
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.Message);
            }

            return license;
        }

        public bool IsFeatureLicensed(string feature)
        {
            try
            {
                if (!HasErrors)
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
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.Message);
            }

            return false;
        }

        public bool IsFeatureCount(string feature, int count)
        {
            try
            {
                if (!HasErrors)
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
