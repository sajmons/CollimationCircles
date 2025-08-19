using CollimationCircles.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.ComponentModel;
using HanumanInstitute.MvvmDialogs;
using System.Threading.Tasks;

namespace CollimationCircles.ViewModels
{
    internal partial class AIViewModel : BaseViewModel
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private readonly ILibVLCService libVLCService;
        private readonly IAIService aIService;

        private INotifyPropertyChanged? dialog;        

        [ObservableProperty]
        private string apiKey = string.Empty;

        [ObservableProperty]
        private bool isValidOpenApiKey = false;        

        public AIViewModel(ILibVLCService libVLCService, IAIService aIService)
        {            
            this.libVLCService = libVLCService ?? throw new ArgumentNullException(nameof(libVLCService));
            this.aIService = aIService ?? throw new ArgumentNullException(nameof(aIService));
        }

        [RelayCommand]
        internal async Task SendToAI()
        {
            if(!libVLCService.MediaPlayer.IsPlaying)
            {
                await DialogService.ShowMessageBoxAsync(null, "Camera stream not running. Please start camera first");
                return;
            }

            libVLCService.TakeSnapshot();
            //string result = await aIService.AnalyzeImageAsync(ApiKey, $".\\{LibVLCService.SnapshotImageFile}");
            string result = await aIService.AnalyzeCollimationWithGeminiAsync(ApiKey, $".\\{LibVLCService.SnapshotImageFile}");
            

            logger.Info("AI analysis result: {0}", result);
        }

        [RelayCommand]
        internal void GetOpenApiKey()
        {
            AppService.OpenUrl("https://platform.openai.com/account/api-keys");
        }

        [RelayCommand]
        internal void SetOpenApiKey()
        {
            OpenAiApiKey = ApiKey;
        }

        partial void OnApiKeyChanged(string value)
        {
            IsValidOpenApiKey = !string.IsNullOrWhiteSpace(value);
        }
    }
}