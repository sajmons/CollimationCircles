using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using CollimationCircles.Extensions;
using CollimationCircles.Helper;
using CollimationCircles.Messages;
using CollimationCircles.Models;
using CollimationCircles.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using HanumanInstitute.MvvmDialogs;
using HanumanInstitute.MvvmDialogs.FrameworkDialogs;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace CollimationCircles.ViewModels
{
    [JsonObject(MemberSerialization.OptIn)]
    public partial class SettingsViewModel : BaseViewModel, IViewClosed
    {
        private readonly IDialogService dialogService;
        private readonly IAppService? appService;

        [ObservableProperty]
        private INotifyPropertyChanged? settingsDialogViewModel;

        [JsonProperty]
        [ObservableProperty]
        private PixelPoint mainWindowPosition = new(100, 100);

        [JsonProperty]
        [ObservableProperty]
        private double mainWindowWidth = 900;

        [JsonProperty]
        [ObservableProperty]
        private double mainWindowHeight = 700;

        [JsonProperty]
        [ObservableProperty]
        private PixelPoint settingsWindowPosition = new(100, 100);

        [JsonProperty]
        [ObservableProperty]
        private double settingsWindowWidth = 560;

        [JsonProperty]
        [ObservableProperty]
        private double settingsWindowHeight = 600;

        [JsonProperty]
        [ObservableProperty]
        [Range(0, 4)]
        [NotifyDataErrorInfo]
        private double scale = 1.0;

        [JsonProperty]
        [ObservableProperty]
        private double labelSize = 10;

        [JsonProperty]
        [ObservableProperty]
        [Range(-180, 180)]
        [NotifyDataErrorInfo]
        private double rotationAngle = 0;

        [JsonProperty]
        [ObservableProperty]
        public bool showLabels = true;

        [JsonProperty]
        [ObservableProperty]
        private ObservableCollection<CollimationHelper> items = new();

        [JsonProperty]
        [ObservableProperty]
        private ObservableCollection<Color> colorList = new();

        [ObservableProperty]
        private CollimationHelper selectedItem = new();

        [JsonProperty]
        [ObservableProperty]
        private int selectedIndex = 0;

        [ObservableProperty]
        private ObservableCollection<KeyValuePair<string, string>> languageList = new();

        [JsonProperty]
        [ObservableProperty]
        private KeyValuePair<string, string> selectedLanguage = new();

        [ObservableProperty]
        private ObservableCollection<string> themeList = new();

        [JsonProperty]
        [ObservableProperty]
        private string selectedTheme = "Dark";

        [JsonProperty]
        [ObservableProperty]
        private bool checkForNewVersionOnStartup = true;

        [JsonProperty]
        [ObservableProperty]
        private bool alwaysOnTop = true;

        [JsonProperty]
        [ObservableProperty]
        private bool dockInMainWindow = true;

        [JsonProperty]
        [ObservableProperty]
        private bool showMarkAtSelectedItem = true;

        [ObservableProperty]
        private string version = string.Empty;

        [ObservableProperty]
        private int settingsMinWidth = 280;

        [ObservableProperty]
        private int settingsWidth = 255;

        [ObservableProperty]
        private string? appDescription;

        [JsonProperty]
        [ObservableProperty]
        [Range(-1000, 1000)]
        private int globalOffsetX = -0;

        [JsonProperty]
        [ObservableProperty]
        [Range(-1000, 1000)]
        private int globalOffsetY = 0;

        [JsonProperty]
        [ObservableProperty]
        [Range(0.1, 1)]
        private double mainWindowOpacity = 0.8;

        public SettingsViewModel(IDialogService dialogService, IAppService appService)
        {
            this.dialogService = dialogService;
            this.appService = appService;

            Initialize();
        }

        public void Initialize()
        {
            if (this.appService is not null)
            {
                InitializeLanguage();
                InitializeThemes();
                InitializeColors();
            }

            Title = $"{DynRes.TryGetString("CollimationCircles")} - {DynRes.TryGetString("Version")} {appService?.GetAppVersion()}";
            AppDescription = $"{DynRes.TryGetString("AppDescription")}\n{DynRes.TryGetString("Copyright")} {DynRes.TryGetString("Author")}";
        }

        private void InitializeThemes()
        {
            // initialize languages
            List<string> l = new()
            {
                nameof(ThemeVariant.Light),
                nameof(ThemeVariant.Dark),
                nameof(Themes.Custom.Night)
            };

            ThemeList = new ObservableCollection<string>(l);
            SelectedTheme = ThemeList.FirstOrDefault() ?? nameof(ThemeVariant.Dark);

            Translate(SelectedLanguage.Value);
        }

        private void InitializeLanguage()
        {
            // initialize languages
            List<KeyValuePair<string, string>> l = new()
            {
                new KeyValuePair<string, string>("English", "en-US"),
                new KeyValuePair<string, string>("Slovenian", "sl-SI"),
                new KeyValuePair<string, string>("German", "de-DE"),
                new KeyValuePair<string, string>("French", "fr-FR")
            };

            LanguageList = new ObservableCollection<KeyValuePair<string, string>>(l);
            SelectedLanguage = LanguageList.FirstOrDefault();

            Translate(SelectedLanguage.Value);
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
            List<CollimationHelper> list = new()
                {
                    // Circles
                    new CircleViewModel() { ItemColor = Colors.LightGreen, Radius = 100, Thickness = 2, Label = DynRes.TryGetString("Secondary") },
                    new CircleViewModel() { ItemColor = Colors.LightBlue, Radius = 250, Thickness = 3, Label = DynRes.TryGetString("FocuserTube") },

                    // Spider
                    new SpiderViewModel(),

                    // Screws
                    new ScrewViewModel(),

                    // Primary Clip
                    new PrimaryClipViewModel(),

                    // Focus mask
                    new BahtinovMaskViewModel()
                };

            Items.Clear();
            Items.AddRange(list);

            SelectedIndex = 0;

            RotationAngle = 0;
            Scale = 1;
            ShowLabels = true;
            CheckForNewVersionOnStartup = true;
            AlwaysOnTop = true;
            DockInMainWindow = true;
            ShowMarkAtSelectedItem = true;

            Version = appService?.GetAppVersion() ?? "0.0.0";
        }

        [RelayCommand]
        internal void ShowSettings()
        {
            if (SettingsDialogViewModel is null)
            {
                SettingsDialogViewModel = dialogService?.CreateViewModel<SettingsViewModel>();

                if (SettingsDialogViewModel is not null)
                {
                    dialogService?.Show(null, SettingsDialogViewModel);
                }

                DockInMainWindow = false;
            }
        }

        [RelayCommand]
        internal void AddCircle()
        {
            AddItem(new CircleViewModel());
        }

        [RelayCommand]
        internal void AddScrew()
        {
            AddItem(new ScrewViewModel());
        }

        [RelayCommand]
        internal void AddClip()
        {
            AddItem(new PrimaryClipViewModel());
        }

        [RelayCommand]
        internal void AddSpider()
        {
            AddItem(new SpiderViewModel());
        }

        [RelayCommand]
        internal void AddBahtinovMask()
        {
            AddItem(new BahtinovMaskViewModel());
        }

        [RelayCommand]
        internal void RemoveItem(CollimationHelper item)
        {
            Items.Remove(item);
            SelectedIndex = Items.Count - 1;
        }

        private void AddItem(CollimationHelper item)
        {
            Items.Add(item);
            SelectedIndex = Items.Count - 1;
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
                Title = DynRes.TryGetString("SaveFile"),
                InitialDirectory = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal),
                Filters = new List<FileFilter>()
                {
                    new FileFilter(DynRes.TryGetString("JSONDocuments"), DynRes.TryGetString("StarJson")),
                    new FileFilter(DynRes.TryGetString("AllFiles"), DynRes.TryGetString("StarChar"))
                },
                DefaultExtension = DynRes.TryGetString("StarJson")
            };

            var path = await dialogService.ShowSaveFileDialogAsync(this, settings);

            if (!string.IsNullOrWhiteSpace(path?.Path?.LocalPath))
            {
                appService?.SaveState(this, path?.Path?.LocalPath);
            }
        }

        [RelayCommand]
        internal async Task LoadList()
        {
            var settings = new OpenFileDialogSettings
            {
                Title = DynRes.TryGetString("OpenFile"),
                InitialDirectory = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal),
                Filters = new List<FileFilter>()
                {
                    new FileFilter(DynRes.TryGetString("JSONDocuments"), DynRes.TryGetString("StarJson")),
                    new FileFilter(DynRes.TryGetString("AllFiles"), DynRes.TryGetString("StarChar")),
                }
            };

            var path = await dialogService.ShowOpenFileDialogAsync(this, settings);

            if (!string.IsNullOrWhiteSpace(path?.Path?.LocalPath))
            {
                if (!LoadState(path: path?.Path?.LocalPath))
                {
                    await dialogService.ShowMessageBoxAsync(this, DynRes.TryGetString("UnableToOpenFile"), DynRes.TryGetString("Error"));
                }
            }
        }

        [RelayCommand]
        internal void Duplicate(int index)
        {
            var selected = Items[index];
            CollimationHelper? c = null;

            switch (selected)
            {
                case CircleViewModel:
                    c = new CircleViewModel();
                    break;
                case PrimaryClipViewModel:
                    c = new PrimaryClipViewModel();
                    break;
                case ScrewViewModel:
                    c = new ScrewViewModel();
                    break;
                case SpiderViewModel:
                    c = new SpiderViewModel();
                    break;
                case BahtinovMaskViewModel:
                    c = new BahtinovMaskViewModel();
                    break;
            }

            if (c is not null)
                Items.Add(c);

            SelectedIndex = Items.Count - 1;
        }

        public void OnClosed()
        {
            SettingsDialogViewModel = null;
        }

        [RelayCommand]
        internal async Task CheckForUpdate()
        {
            string? appVersion = appService?.GetAppVersion();

            if (appVersion is not null)
            {
                if (appService is not null)
                {
                    var (success, result, newVersion) = await appService.DownloadUrl(appVersion);

                    if (success && !string.IsNullOrWhiteSpace(result))
                    {
                        var dialogResult = await dialogService.ShowMessageBoxAsync(null,
                            DynRes.TryGetString("NewVersionDownload").F(newVersion), DynRes.TryGetString("NewVersion"), MessageBoxButton.YesNo);

                        if (dialogResult is true)
                        {
                            OpenUrl(result);
                        }
                    }
                    else if (!success)
                    {
                        await dialogService.ShowMessageBoxAsync(null, result, DynRes.TryGetString("Error"));
                    }
                }
            }
        }

        internal void SaveState()
        {            
            appService?.SaveState(this);
        }

        internal bool LoadState(string? path = null)
        {
            try
            {
                SettingsViewModel? vm = appService?.LoadState<SettingsViewModel>(path);

                if (vm != null && vm.Items != null)
                {
                    MainWindowPosition = vm.MainWindowPosition;
                    MainWindowWidth = vm.MainWindowWidth;
                    MainWindowHeight = vm.MainWindowHeight;
                    
                    Scale = vm.Scale;
                    RotationAngle = vm.RotationAngle;
                    ShowLabels = vm.ShowLabels;
                    ColorList = vm.ColorList;
                    LabelSize = vm.LabelSize;

                    Items.Clear();
                    Items.AddRange(vm.Items);

                    SelectedIndex = vm.SelectedIndex;

                    SelectedLanguage = vm.SelectedLanguage;
                    SelectedTheme = vm.SelectedTheme;
                    CheckForNewVersionOnStartup = vm.CheckForNewVersionOnStartup;
                    AlwaysOnTop = vm.AlwaysOnTop;
                    DockInMainWindow = vm.DockInMainWindow;
                    ShowMarkAtSelectedItem = vm.ShowMarkAtSelectedItem;
                    Version = vm.Version ?? appService?.GetAppVersion() ?? "0.0.0";
                    GlobalOffsetX = vm.GlobalOffsetX;
                    GlobalOffsetY = vm.GlobalOffsetY;
                    MainWindowOpacity = vm.MainWindowOpacity;
                    
                    SettingsWindowPosition = vm.SettingsWindowPosition;
                    SettingsWindowWidth = vm.SettingsWindowWidth;
                    SettingsWindowHeight = vm.SettingsWindowHeight;

                    if (!DockInMainWindow)
                    {
                        ShowSettings();
                    }
                }
                else
                {
                    InitializeDefaults();
                    return false;
                }

                return true;
            }
            catch
            {
                InitializeDefaults();
                return false;
            }
        }

        partial void OnSelectedLanguageChanged(KeyValuePair<string, string> value)
        {
            if (SelectedLanguage.Value is not null)
            {
                Translate(SelectedLanguage.Value);
            }
        }

        partial void OnSelectedThemeChanged(string value)
        {
            if (SelectedTheme is not null)
            {
                if (Application.Current is not null)
                {
                    switch (value)
                    {
                        default:
                            Application.Current.RequestedThemeVariant = ThemeVariant.Default;
                            break;
                        case nameof(ThemeVariant.Light):
                            Application.Current.RequestedThemeVariant = ThemeVariant.Light;
                            break;
                        case nameof(Themes.Custom.Night):
                            Application.Current.RequestedThemeVariant = Themes.Custom.Night;
                            break;
                        case nameof(ThemeVariant.Dark):
                            Application.Current.RequestedThemeVariant = ThemeVariant.Dark;
                            break;
                    };
                }
            }
        }

        [RelayCommand]
        internal void OpenWebSite()
        {
            if (appService is not null)
            {
                OpenUrl(appService.WebPage);
            }
        }

        [RelayCommand]
        internal void OpenContactWebPage()
        {
            if (appService is not null)
            {
                OpenUrl(appService.ContactPage);
            }
        }

        [RelayCommand]
        internal void OpenGitHubPage()
        {
            if (appService is not null)
            {
                OpenUrl(appService.GitHubPage);
            }
        }

        [RelayCommand]
        internal void OpenTwitter()
        {
            if (appService is not null)
            {
                OpenUrl(appService.TwitterPage);
            }
        }

        [RelayCommand]
        internal void OpenYouTubeChannel()
        {
            if (appService is not null)
            {
                OpenUrl(appService.YouTubeChannel);
            }
        }

        
        [RelayCommand]
        internal void GitHubIssue()
        {
            if (appService is not null)
            {
                OpenUrl(appService.GitHubIssue);
            }
        }

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(RotationAngle):
                case nameof(Scale):
                case nameof(LabelSize):
                case nameof(ShowLabels):
                case nameof(AlwaysOnTop):
                case nameof(SelectedIndex):
                case nameof(ShowMarkAtSelectedItem):
                case nameof(DockInMainWindow):
                case nameof(GlobalOffsetX):
                case nameof(GlobalOffsetY):
                case nameof(MainWindowOpacity):
                    if (!HasErrors)
                    {
                        base.OnPropertyChanged(e);
                        WeakReferenceMessenger.Default.Send(new SettingsChangedMessage(this));
                    }
                    break;
            }
        }
    }
}
