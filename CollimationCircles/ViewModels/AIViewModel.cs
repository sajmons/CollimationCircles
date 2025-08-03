using Avalonia;
using CollimationCircles.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using OpenAI.Assistants;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Files;
using System;
using System.ClientModel;
using System.ComponentModel;
using TextCopy;
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
            aIService.AnalyzeImage(ApiKey, $".\\{LibVLCService.SnapshotImageFile}");
        }

        [RelayCommand]
        internal void GetOpenApiKey()
        {
            AppService.OpenUrl("https://platform.openai.com/account/api-keys");
        }

        [RelayCommand]
        internal void SetOpenApiKey()
        {
            OpenApiKey = ApiKey;
        }

        partial void OnApiKeyChanged(string value)
        {
            IsValidOpenApiKey = !string.IsNullOrWhiteSpace(value);
        }
    }
}