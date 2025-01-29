﻿using CollimationCircles.Messages;
using CollimationCircles.Services;
using CommunityToolkit.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using HanumanInstitute.MvvmDialogs;
using HanumanInstitute.MvvmDialogs.FileSystem;
using HanumanInstitute.MvvmDialogs.FrameworkDialogs;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using static CollimationCircles.Services.ImageAnalysisService;

namespace CollimationCircles.ViewModels
{
    public partial class CollimationAnalysisViewModel : BaseViewModel
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private readonly ILibVLCService libVLCService;

        private string lastPath = ".\\";

        private Mat lastImage = new();

        [ObservableProperty]
        bool isPlaying = false;

        [ObservableProperty]
        bool isCircleHoughTransform = true;

        [ObservableProperty]
        bool isContourMinimumEnclosingCircle = false;

        [ObservableProperty]
        bool isDebug = false;        

        public CollimationAnalysisViewModel(ILibVLCService libVLCService)
        {
            this.libVLCService = libVLCService;

            WeakReferenceMessenger.Default.Register<CameraStateMessage>(this, (r, m) =>
            {
                switch (m.Value)
                {
                    case CameraState.Stopped:
                        IsPlaying = false;
                        break;
                    case CameraState.Playing:
                        IsPlaying = true;
                        break;
                }
            });
        }

        [RelayCommand]
        private async Task TakeSnapshot()
        {
            Guard.IsNotNull(libVLCService);
            Guard.IsTrue(libVLCService.MediaPlayer.IsPlaying);

            libVLCService.TakeSnapshot();

            lastImage = ImageAnalysisService.LoadImage($"{LibVLCService.SnapshotImageFile}", ImreadModes.Color);

            AnalysisType at = AnalysisType.CircleHoughTransform;

            if (IsContourMinimumEnclosingCircle)
                at = AnalysisType.ContourMinimumEnclosingCircle;

            await DoAnalysis(lastImage, at);
        }

        [RelayCommand]
        public async Task LoadImage()
        {
            var settings = new OpenFileDialogSettings
            {
                SuggestedFileName = "*.jpg",
                SuggestedStartLocation = new DesktopDialogStorageFolder(new DirectoryInfo(lastPath)),
                Title = ResSvc.TryGetString("OpenFile"),
                Filters =
                [
                    new("JPEG", "jpg")
                ]
            };

            var path = await DialogService.ShowOpenFileDialogAsync(null, settings);

            if (!string.IsNullOrWhiteSpace(path?.Path?.LocalPath))
            {
                lastPath = $"{path?.Path?.LocalPath}";

                lastImage = ImageAnalysisService.LoadImage(lastPath, ImreadModes.Color);

                AnalysisType at = AnalysisType.CircleHoughTransform;

                if (IsContourMinimumEnclosingCircle)
                    at = AnalysisType.ContourMinimumEnclosingCircle;

                await DoAnalysis(lastImage, at);
            }            
        }

        private async Task DoAnalysis(Mat image, AnalysisType analysisType)
        {
            Options options = new()
            {
                ShowEachImage = IsDebug,
                DoNormalize = true,
                DoGaussianBlur = true,
                DoAdaptiveThreshold = true,
                DoMorphologyEx = true,
                DoCanny = true
            };

            Mat processed = ImageAnalysisService.ProcessImage(image, options);

            List<CircleSegment> circles;
            AnalysisResult result = new();

            switch (analysisType)
            {
                case AnalysisType.CircleHoughTransform:
                    circles = ImageAnalysisService.DetectHoughCircles(processed, options);
                    result = ImageAnalysisService.AnalyzeResult(image, circles, options);
                    break;
                case AnalysisType.ContourMinimumEnclosingCircle:
                    circles = ImageAnalysisService.DetectCirclesFromContour(processed, options);
                    result = ImageAnalysisService.AnalyzeResult(image, circles, options);
                    break;            
            }            

            // Display the result
            Cv2.ImShow("Detected Circles", image);
            await DescribeResult(result, options);
        }

        private async Task DescribeResult(AnalysisResult result, Options options)
        {
            string message = $"Offset from optical axis: {result.Offset}px" + Environment.NewLine + Environment.NewLine;

            if (result?.Offset == -1)
            {
                message = $"Unable to detect defocused star.\nPlease point your telescope to bright star and then defocuse it.";
            }
            else
            {
                if (result?.CircleCount < options.MinCirclesDetected)
                {
                    message += $"Number of detected circles is to small.";
                }
                else if (result?.Offset < options.OffsetLimit)
                {
                    message += $"Telescope is likely well-collimated.";
                }
                else
                {
                    message += "Collimation issues detected.";
                }
            }

            await DialogService.ShowMessageBoxAsync(null, $"{message}\nNumber of circles: {result?.CircleCount}\n{result?.Error}", "Collimation analysis", MessageBoxButton.Ok);
        }

        partial void OnIsCircleHoughTransformChanged(bool value)
        {
            AnalysisType at = AnalysisType.CircleHoughTransform;

            if (IsContourMinimumEnclosingCircle)
                at = AnalysisType.ContourMinimumEnclosingCircle;

            Task.Run(async () => await DoAnalysis(lastImage, at));
        }

        partial void OnIsContourMinimumEnclosingCircleChanged(bool value)
        {
            AnalysisType at = AnalysisType.CircleHoughTransform;

            if (IsContourMinimumEnclosingCircle)
                at = AnalysisType.ContourMinimumEnclosingCircle;

            Task.Run(async () => await DoAnalysis(lastImage, at));
        }
    }
}
