using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static CollimationCircles.Services.OpticalAxisService;

namespace CollimationCircles.ViewModels
{
    public partial class ScrewCalibrationViewModel : BaseViewModel
    {
        private const int OffsetSamples = 3;
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        private const double CalibrationTurn = 0.125;
        private const double MinCalibrationDelta = 0.003;

        private readonly SettingsViewModel settingsViewModel;

        private readonly object offsetLock = new();
        private readonly Queue<(double X, double Y)> recentOffsets = new();
        private List<(double X, double Y)>? screwResponse;
        private List<(double X, double Y)> pendingResponse = [];
        private (double X, double Y)? calibrationBaseline;
        private bool calibratedWithAdvanced;
        private double calibratedRotation;

        public Func<bool> IsAnyLiveAnalysisActive { get; set; } = () => false;

        public Func<bool> IsAdvancedAnalysisActive { get; set; } = () => false;

        public event Action? CalibrationChanged;

        [ObservableProperty]
        bool isCalibrating;

        [ObservableProperty]
        bool hasCalibration;

        [ObservableProperty]
        string sourceText = "Guidance: estimated (not calibrated)";

        partial void OnHasCalibrationChanged(bool value) =>
            SourceText = value ? "Guidance: calibrated" : "Guidance: estimated (not calibrated)";

        [ObservableProperty]
        string calibrationMessage = string.Empty;

        public ScrewCalibrationViewModel()
        {
            settingsViewModel = Ioc.Default.GetRequiredService<SettingsViewModel>();
        }

        public void InvalidateIfScrewChanged()
        {
            var response = screwResponse;
            if (response is null)
            {
                return;
            }

            var screwVM = settingsViewModel.Items.OfType<ScrewViewModel>().FirstOrDefault();
            if (screwVM is not null && screwVM.Count == response.Count && Math.Abs(screwVM.RotationAngle - calibratedRotation) < 0.001)
            {
                return;
            }

            logger.Info("Calibration invalidated: screw settings changed (count={0}, rotation={1:F1}, calibrated rotation={2:F1})",
                screwVM?.Count, screwVM?.RotationAngle, calibratedRotation);
            screwResponse = null;
            HasCalibration = false;
            CalibrationMessage = "Screw settings changed. Please recalibrate.";
            CalibrationChanged?.Invoke();
        }

        public List<(double X, double Y)>? GetResponse(int screwCount, bool advancedActive)
        {
            var response = screwResponse;
            return response is not null && response.Count == screwCount && calibratedWithAdvanced == advancedActive
                ? response
                : null;
        }

        public void RecordOffset(Dictionary<string, double>? wavefront)
        {
            lock (offsetLock)
            {
                if (wavefront is null)
                {
                    return;
                }

                double rad = wavefront["ComaAngle"] * Math.PI / 180.0;
                double mag = wavefront["ComaMagnitude"];
                recentOffsets.Enqueue((mag * Math.Cos(rad), mag * Math.Sin(rad)));
                while (recentOffsets.Count > OffsetSamples)
                {
                    recentOffsets.Dequeue();
                }
            }
        }

        private async Task<(double X, double Y)?> MeasureOffsetAsync()
        {
            lock (offsetLock)
            {
                recentOffsets.Clear();
            }

            for (int i = 0; i < 600; i++)
            {
                await Task.Delay(100);
                lock (offsetLock)
                {
                    if (recentOffsets.Count >= OffsetSamples)
                    {
                        return (recentOffsets.Average(o => o.X), recentOffsets.Average(o => o.Y));
                    }
                }
            }

            return null;
        }

        [RelayCommand]
        private void StartCalibration()
        {
            var screwVM = settingsViewModel.Items.OfType<ScrewViewModel>().FirstOrDefault();
            if (screwVM is null || !IsAnyLiveAnalysisActive())
            {
                CalibrationMessage = screwVM is null
                    ? "Add a screw item first."
                    : "Start live analysis (defocused star or optical tube) first.";
                return;
            }

            pendingResponse = [];
            calibrationBaseline = null;
            logger.Info("Calibration started: screws={0}", screwVM.Count);
            IsCalibrating = true;
            CalibrationMessage = "Do not touch the telescope, then press Capture to record the start position.";
        }

        [RelayCommand]
        private async Task CaptureCalibrationAsync()
        {
            var screwVM = settingsViewModel.Items.OfType<ScrewViewModel>().FirstOrDefault();
            if (screwVM is null)
            {
                CancelCalibration();
                return;
            }

            CalibrationMessage = "Measuring...";
            var current = await MeasureOffsetAsync();
            if (current is null)
            {
                CalibrationMessage = "No measurement: the analysis must detect both circles (inner and outer) for several frames. Adjust exposure/gain or nudge a screw so both circles are detected, then press Capture again.";
                return;
            }

            if (calibrationBaseline is null)
            {
                calibrationBaseline = current;
                logger.Info("Calibration baseline offset=({0:F4},{1:F4})", current.Value.X, current.Value.Y);
                CalibrationMessage = "Turn the screw labelled 'Primary screw 1' on the image about 1/8 turn (45°) clockwise (tightening, seen from the screw head), wait, then press Capture.";
                return;
            }

            var start = calibrationBaseline.Value;
            double dx = current.Value.X - start.X;
            double dy = current.Value.Y - start.Y;
            if (Math.Sqrt(dx * dx + dy * dy) < MinCalibrationDelta)
            {
                CalibrationMessage = $"No change detected. Turn 'Primary screw {pendingResponse.Count + 1}' 1/8 turn clockwise (a little more if needed), wait, then press Capture.";
                return;
            }

            pendingResponse.Add((dx / CalibrationTurn, dy / CalibrationTurn));
            logger.Info("Calibration screw {0}: offset=({1:F4},{2:F4}), delta=({3:F4},{4:F4}), response per turn=({5:F4},{6:F4})",
                pendingResponse.Count, current.Value.X, current.Value.Y, dx, dy, dx / CalibrationTurn, dy / CalibrationTurn);
            calibrationBaseline = current;

            if (pendingResponse.Count < screwVM.Count)
            {
                CalibrationMessage = $"Screw {pendingResponse.Count} recorded. Now turn 'Primary screw {pendingResponse.Count + 1}' about 1/8 turn clockwise, wait, then press Capture.";
                return;
            }

            if (CalculateCalibratedAdjustments(0.1, 0, pendingResponse) is null)
            {
                IsCalibrating = false;
                CalibrationMessage = "Screw directions are too similar. Check that each turn moved a different screw and repeat.";
                return;
            }

            screwResponse = pendingResponse;
            logger.Info("Calibration complete: advanced={0}, response=[{1}]", IsAdvancedAnalysisActive(),
                string.Join("; ", pendingResponse.Select((r, i) => $"S{i + 1}=({r.X:F4},{r.Y:F4})")));
            calibratedWithAdvanced = IsAdvancedAnalysisActive();
            calibratedRotation = screwVM.RotationAngle;
            HasCalibration = true;
            IsCalibrating = false;
            CalibrationMessage = "Calibrated. Screw numbers match the 'Primary screw N' labels; CW means clockwise.";
            CalibrationChanged?.Invoke();
        }

        [RelayCommand]
        private void CancelCalibration()
        {
            IsCalibrating = false;
            calibrationBaseline = null;
            pendingResponse = [];
            CalibrationMessage = string.Empty;
        }

        [RelayCommand]
        private void ClearCalibration()
        {
            screwResponse = null;
            logger.Info("Calibration cleared");
            HasCalibration = false;
            CalibrationMessage = string.Empty;
            CalibrationChanged?.Invoke();
        }
    }
}
