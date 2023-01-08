using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using CollimationCircles.Extensions;
using CollimationCircles.Messages;
using CollimationCircles.Models;
using CollimationCircles.Resources.Strings;
using CollimationCircles.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using DynamicData;
using HanumanInstitute.MvvmDialogs;
using HanumanInstitute.MvvmDialogs.FrameworkDialogs;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace CollimationCircles.ViewModels
{
    [JsonObject(MemberSerialization.OptIn)]
    public partial class SettingsViewModel : BaseViewModel, IViewClosed
    {
        private readonly IDialogService dialogService;
        private readonly IAppService appService;

        [ObservableProperty]
        private INotifyPropertyChanged? dialogViewModel;

        [JsonProperty]
        [ObservableProperty]
        public PixelPoint position = new(100, 100);

        [JsonProperty]
        [ObservableProperty]
        public double width = 650;

        [JsonProperty]
        [ObservableProperty]
        public double height = 650;

        [JsonProperty]
        [ObservableProperty]
        public double scale = 1.0;

        [JsonProperty]
        [ObservableProperty]
        public double rotationAngle = 0;

        [JsonProperty]
        [ObservableProperty]
        public bool showLabels = true;

        [JsonProperty]
        [ObservableProperty]
        public ObservableCollection<CollimationHelper> items = new();

        [JsonProperty]
        [ObservableProperty]
        public ObservableCollection<Color> colorList = new();

        [ObservableProperty]
        public CollimationHelper selectedItem = new();

        [ObservableProperty]
        public bool isSelectedItem = false;

        [ObservableProperty]
        public ObservableCollection<KeyValuePair<string, string>> languageList = new();

        [JsonProperty]
        [ObservableProperty]
        public KeyValuePair<string, string> selectedLanguage = new();

        [ObservableProperty]
        public int selectedLanguageIndex = 0;

        [JsonProperty]
        [ObservableProperty]
        public bool checkForNewVersionOnStartup = true;

        public SettingsViewModel(IDialogService dialogService, IAppService appService)
        {
            this.dialogService = dialogService;
            this.appService = appService;

            InitializeColors();
            InitializeDefaults();
            InitializeMessages();
            InitializeLanguages();

            Title = $"{Text.CollimationCircles} - {Text.Settings} - {Text.Version} {appService?.GetAppVersion()}";
            MainTitle = $"{Text.CollimationCircles} - {Text.Version} {appService?.GetAppVersion()}";
        }

        private void InitializeMessages()
        {
            WeakReferenceMessenger.Default.Register<ItemChangedMessage>(this, (r, m) =>
            {
                WeakReferenceMessenger.Default.Send(new SettingsChangedMessage(this));
            });
        }

        private void InitializeColors()
        {
            List<Color> c = new()
            {
                Colors.Red,
                Colors.Green,
                Colors.Blue,
                Colors.Orange,
                Colors.LightBlue,
                Colors.LightGreen,
                Colors.Yellow,
                Colors.Fuchsia,
                Colors.Cyan,
                Colors.Lime,
                Colors.Gold,
                Colors.White,
                Colors.Black
            };

            ColorList = new ObservableCollection<Color>(c);
        }

        private void InitializeDefaults()
        {
            if (Items is not null)
            {
                List<CollimationHelper> list = new()
                {
                    // Circles
                    new CircleViewModel() { ItemColor = Colors.LightGreen, Radius = 100, Thickness = 2, Label = Text.Inner },
                    new CircleViewModel() { ItemColor = Colors.LightBlue, Radius = 250, Thickness = 3, Label = Text.PrimaryOuter },

                    // Spider
                    new SpiderViewModel(),

                    // Screws
                    new ScrewViewModel(),

                    // Primary Clip
                    new PrimaryClipViewModel()
                };

                Items.Clear();
                Items.AddRange(list);

                Items.CollectionChanged += Items_CollectionChanged;

                SelectedItem = Items?.FirstOrDefault()!;
            }

            RotationAngle = 0;
            Scale = 1;
            ShowLabels = true;
            SelectedLanguage = LanguageList.FirstOrDefault();
        }

        private void InitializeLanguages()
        {
            List<KeyValuePair<string, string>> l = new()
            {
                new KeyValuePair<string, string>(Text.English, "en-US"),
                new KeyValuePair<string, string>(Text.Slovenian, "sl-SI")
            };

            LanguageList = new ObservableCollection<KeyValuePair<string, string>>(l);
            SelectedLanguage = LanguageList.FirstOrDefault();
        }

        private void Items_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            WeakReferenceMessenger.Default.Send(new SettingsChangedMessage(this));
        }

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            WeakReferenceMessenger.Default.Send(new SettingsChangedMessage(this));
        }

        [RelayCommand]
        internal void ShowSettings()
        {
            if (DialogViewModel is null)
            {
                DialogViewModel = dialogService.CreateViewModel<SettingsViewModel>();
                dialogService.Show(null, DialogViewModel);
            }
        }

        [RelayCommand]
        internal void CloseSettings()
        {
            dialogService.Close(DialogViewModel!);
            DialogViewModel = null;
        }

        [RelayCommand]
        internal void AddCircle()
        {
            Items?.Add(new CircleViewModel());
        }

        [RelayCommand]
        internal void AddScrew()
        {
            Items?.Add(new ScrewViewModel());
        }

        [RelayCommand]
        internal void AddClip()
        {
            Items?.Add(new PrimaryClipViewModel());
        }

        [RelayCommand]
        internal void AddSpider()
        {
            Items?.Add(new SpiderViewModel());
        }

        [RelayCommand]
        internal void RemoveItem(CollimationHelper item)
        {
            Items?.Remove(item);
        }

        [RelayCommand]
        internal void ResetList()
        {
            InitializeDefaults();
        }

        [RelayCommand]
        internal async Task SaveList()
        {
            var settings = new SaveFileDialogSettings
            {
                Title = Text.SaveFile,
                InitialDirectory = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal),
                Filters = new List<FileFilter>()
                {
                    new FileFilter(Text.JSONDocuments, Text.StarJson),
                    new FileFilter(Text.AllFiles, Text.StarChar)
                },
                DefaultExtension = Text.StarJson
            };

            var result = await dialogService.ShowSaveFileDialogAsync(this, settings);

            var path = result?.Path?.LocalPath;

            if (!string.IsNullOrWhiteSpace(path))
            {
                appService.SaveState(this, path);
            }
        }

        [RelayCommand]
        internal async Task LoadList()
        {
            var settings = new OpenFileDialogSettings
            {
                Title = Text.OpenFile,
                InitialDirectory = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal),
                Filters = new List<FileFilter>()
                {
                    new FileFilter(Text.JSONDocuments, Text.StarJson),
                    new FileFilter(Text.AllFiles, Text.StarChar),
                }
            };

            var result = await dialogService.ShowOpenFileDialogAsync(this, settings);

            string? path = result?.Path?.LocalPath;

            if (!string.IsNullOrWhiteSpace(path))
            {
                if (!LoadState(path))
                {
                    await dialogService.ShowMessageBoxAsync(this, Text.UnableToOpenFile, Text.Error);
                }
            }
        }

        partial void OnSelectedItemChanged(CollimationHelper value)
        {
            IsSelectedItem = SelectedItem is not null;
        }

        partial void OnSelectedLanguageIndexChanged(int value)
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo(SelectedLanguage.Value);
            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(SelectedLanguage.Value);

            Task.Run(async () =>
            {
                var dialogResult = await dialogService.ShowMessageBoxAsync(this, Text.WindowRestart, Text.Error, MessageBoxButton.YesNo);
                
                if (dialogResult is true)
                {
                    if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                    {
                        desktop.Shutdown();
                    }
                }
            });
        }

        public void OnClosed()
        {
            DialogViewModel = null;
        }

        [RelayCommand]
        internal async Task CheckForUpdate()
        {
            var (downloadUrl, newVersion) = await appService.DownloadUrl(appService.GetAppVersion());

            if (!string.IsNullOrWhiteSpace(downloadUrl))
            {
                var dialogResult = await dialogService.ShowMessageBoxAsync(this, Text.NewVersionDownload.F(newVersion), Text.NewVersion, MessageBoxButton.YesNo);

                if (dialogResult is true)
                {
                    OpenUrl(downloadUrl);
                }
            }
        }

        private void OpenUrl(string url)
        {
            try
            {
                Process.Start(url);
            }
            catch
            {
                // hack because of this: https://github.com/dotnet/corefx/issues/10361
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    url = url.Replace("&", "^&");
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    Process.Start("xdg-open", url);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Process.Start("open", url);
                }
                else
                {
                    throw;
                }
            }
        }

        internal void SaveState()
        {
            appService.SaveState<SettingsViewModel>(this);
        }

        internal bool LoadState(string? path = null)
        {
            try
            {
                SettingsViewModel? vm = appService?.LoadState<SettingsViewModel>(path);

                if (vm != null && vm.Items != null)
                {
                    Position = vm.Position;
                    Width = vm.Width;
                    Height = vm.Height;
                    Scale = vm.Scale;
                    RotationAngle = vm.RotationAngle;
                    ShowLabels = vm.ShowLabels;
                    ColorList = vm.ColorList;

                    Items?.Clear();
                    Items?.AddRange(vm.Items);

                    SelectedLanguage = vm.SelectedLanguage;
                    CheckForNewVersionOnStartup = vm.CheckForNewVersionOnStartup;
                }
                else
                {
                    return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
