using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using CollimationCircles.Extensions;
using CollimationCircles.Helper;
using CollimationCircles.Messages;
using CollimationCircles.Models;
using CollimationCircles.Services;
using CommunityToolkit.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using HanumanInstitute.MvvmDialogs;
using HanumanInstitute.MvvmDialogs.FrameworkDialogs;
using Newtonsoft.Json;
using System;
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
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        [ObservableProperty]
        private INotifyPropertyChanged? settingsDialogViewModel;

        [JsonProperty]
        [ObservableProperty]
        private PixelPoint mainWindowPosition = new(100, 100);

        [JsonProperty]
        [ObservableProperty]
        private double mainWindowWidth = 1024;

        [JsonProperty]
        [ObservableProperty]
        private double mainWindowHeight = 768;

        [JsonProperty]
        [ObservableProperty]
        private PixelPoint settingsWindowPosition = new(100, 100);

        [JsonProperty]
        [ObservableProperty]
        private double settingsWindowWidth = 580;

        [JsonProperty]
        [ObservableProperty]
        private double settingsWindowHeight = 600;

        [JsonProperty]
        [ObservableProperty]
        [Range(Constraints.ScaleMin, Constraints.ScaleMax)]
        [NotifyDataErrorInfo]
        private double scale = 1.0;

        [JsonProperty]
        [ObservableProperty]
        private double labelSize = 16;

        [JsonProperty]
        [ObservableProperty]
        [Range(Constraints.RotationAngleMin, Constraints.RotationAngleMax)]
        [NotifyDataErrorInfo]
        private double rotationAngle = 0;

        [JsonProperty]
        [ObservableProperty]
        public bool showLabels = true;

        [JsonProperty]
        [ObservableProperty]
        private ObservableCollection<CollimationHelper> items = [];

        [JsonProperty]
        [ObservableProperty]
        private ObservableCollection<Color> colorList = [];

        [ObservableProperty]
        private CollimationHelper selectedItem = new();

        [JsonProperty]
        [ObservableProperty]
        private int selectedIndex = 0;

        [ObservableProperty]
        private ObservableCollection<KeyValuePair<string, string>> languageList = [];

        [JsonProperty]
        [ObservableProperty]
        private KeyValuePair<string, string> selectedLanguage = new();

        [ObservableProperty]
        private ObservableCollection<ThemeVariant> themeList = [];

        [JsonProperty]
        [ObservableProperty]
        private ThemeVariant selectedTheme = ThemeVariant.Default;

        [JsonProperty]
        [ObservableProperty]
        private bool checkForNewVersionOnStartup = true;

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

        [JsonProperty]
        [ObservableProperty]
        [Range(Constraints.OffsetMin, Constraints.OffsetMax)]
        private int globalOffsetX = 0;

        [JsonProperty]
        [ObservableProperty]
        [Range(Constraints.OffsetMin, Constraints.OffsetMax)]
        private int globalOffsetY = 0;

        [JsonProperty]
        [ObservableProperty]
        [Range(Constraints.OpacityMin, Constraints.OpacityMax)]
        private double mainWindowOpacity = 0.8;

        [JsonProperty]
        [ObservableProperty]
        private bool settingsExpanded = true;

        [JsonProperty]
        [ObservableProperty]
        private bool showKeyboardShortcuts = true;

        [JsonProperty]
        [ObservableProperty]
        private bool cameraVideoStreamExpanded = true;

        [JsonProperty]
        [ObservableProperty]
        private bool profileManagerExpanded = true;

        [JsonProperty]
        [ObservableProperty]
        private bool pinVideoWindowToMainWindow = true;

        [JsonProperty]
        [ObservableProperty]
        private bool showApplicationLog = false;

        [JsonProperty]
        [ObservableProperty]
        private bool globalPropertiesExpanded = true;

        [ObservableProperty]
        private Dictionary<string, string> globalShortcuts = [];

        [ObservableProperty]
        private Dictionary<string, string> shapeShortcuts = [];

        [JsonProperty]
        [ObservableProperty]
        private string lastSelectedCamera = string.Empty;

        [JsonProperty]
        [ObservableProperty]
        private ObservableCollection<Profile> profiles = [];        

        protected override void Initialize()
        {
            InitializeLanguage();
            InitializeThemes();
            InitializeColors();
            InitializeKeyboardShortcuts();            
        }

        private void InitializeKeyboardShortcuts()
        {
            GlobalShortcuts = new()
            {
                { ResSvc.TryGetString("GlobalRotationCW"), "CTRL R" },
                { ResSvc.TryGetString("GlobalRotationCCW"), "CTRL F" },
                { ResSvc.TryGetString("GlobalScaleUp"), "CTRL +" },
                { ResSvc.TryGetString("GlobalScaleDown"), "CTRL -" }
            };

            ShapeShortcuts = new()
            {
                { ResSvc.TryGetString("IncreaseHelperRadius"), "CTRL W" },
                { ResSvc.TryGetString("DecreaseHelperRadius"), "CTRL S" },
                { ResSvc.TryGetString("IncreaseItemThichness"), "CTRL E" },
                { ResSvc.TryGetString("DecreaseItemThickness"), "CTRL D" },
                { ResSvc.TryGetString("RotateHelperCW"), "CTRL Q" },
                { ResSvc.TryGetString("RotateHelperCCW"), "CTRL A" },
                { ResSvc.TryGetString("IncreaseInclination"), "CTRL U" },
                { ResSvc.TryGetString("DecreaseInclination"), "CTRL J" },
                { ResSvc.TryGetString("IncreaseHelperSpacing"), "CTRL Z" },
                { ResSvc.TryGetString("DecreaseHelperSpacing"), "CTRL H" },
                { ResSvc.TryGetString("IncreaseHelperCount"), "CTRL T" },
                { ResSvc.TryGetString("DecreaseHelperCount"), "CTRL G" }
            };

            logger.Info("Keyboard shortcuts initialized");
        }

        private void InitializeThemes()
        {
            // initialize themes
            List<ThemeVariant> themes =
            [
                ThemeVariant.Light,
                ThemeVariant.Dark,
                Themes.Custom.Night
            ];

            ThemeList = new ObservableCollection<ThemeVariant>(themes);
            //SelectedTheme = ThemeList.FirstOrDefault() ?? ThemeVariant.Default;

            logger.Info("Initialized themes");
        }

        private void InitializeLanguage()
        {
            // initialize languages
            List<KeyValuePair<string, string>> l =
            [
                new KeyValuePair<string, string>("English", "en-US"),
                new KeyValuePair<string, string>("Slovenian", "sl-SI"),
                new KeyValuePair<string, string>("German", "de-DE"),
                new KeyValuePair<string, string>("French", "fr-FR")
            ];

            LanguageList = new ObservableCollection<KeyValuePair<string, string>>(l);
            //SelectedLanguage = LanguageList.FirstOrDefault();

            logger.Info("Initialized languages");
        }

        private void InitializeColors()
        {
            List<Color> c =
            [
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
            ];

            ColorList = new ObservableCollection<Color>(c);

            logger.Info("Initialized colors");
        }

        private void InitializeDefaults()
        {
            List<CollimationHelper> list =
            [
                // Circles
                new CircleViewModel() { ItemColor = Colors.LightGreen, Radius = 100, Thickness = 2, Label = ResSvc.TryGetString("Secondary") },
                new CircleViewModel() { ItemColor = Colors.LightBlue, Radius = 250, Thickness = 3, Label = ResSvc.TryGetString("FocuserTube") },

                // Spider
                new SpiderViewModel(),

                // Screws
                new ScrewViewModel(),

                // Primary Clip
                new PrimaryClipViewModel()
            ];

            Items.Clear();
            Items.AddRange(list);

            SelectedIndex = 0;

            RotationAngle = 0;
            Scale = 1;
            ShowLabels = true;
            CheckForNewVersionOnStartup = true;
            AlwaysOnTop = true;
            ShowMarkAtSelectedItem = true;
            ShowApplicationLog = false;

            Version = AppService.GetAppVersion();

            logger.Info("Settings reset to default values");
        }

        [RelayCommand]
        internal void ShowSettings()
        {
            if (SettingsDialogViewModel is null)
            {
                SettingsDialogViewModel = DialogService?.CreateViewModel<SettingsViewModel>();

                if (SettingsDialogViewModel is not null)
                {
                    DialogService?.Show(null, SettingsDialogViewModel);
                    logger.Info("Opened settings window");
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
            InCaseOfValidLicense(() =>
            {
                AddItem(new BahtinovMaskViewModel());
            });
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

            logger.Debug($"Added shape {item.Label}");
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
                Title = ResSvc.TryGetString("SaveFile"),
                Filters =
                [
                    new(ResSvc.TryGetString("JSONDocuments"), ResSvc.TryGetString("StarJson")),
                    new(ResSvc.TryGetString("AllFiles"), ResSvc.TryGetString("StarChar"))
                ],
                DefaultExtension = ResSvc.TryGetString("StarJson")
            };

            var path = await DialogService.ShowSaveFileDialogAsync(this, settings);

            if (!string.IsNullOrWhiteSpace(path?.Path?.LocalPath))
            {
                AppService.SaveState(this, path?.Path?.LocalPath);
            }
        }

        [RelayCommand]
        internal async Task LoadList()
        {
            var settings = new OpenFileDialogSettings
            {
                Title = ResSvc.TryGetString("OpenFile"),
                Filters =
                [
                    new(ResSvc.TryGetString("JSONDocuments"), ResSvc.TryGetString("StarJson")),
                    new(ResSvc.TryGetString("AllFiles"), ResSvc.TryGetString("StarChar")),
                ]
            };

            var path = await DialogService.ShowOpenFileDialogAsync(this, settings);

            if (!string.IsNullOrWhiteSpace(path?.Path?.LocalPath))
            {
                if (!LoadState(path: path?.Path?.LocalPath))
                {
                    await DialogService.ShowMessageBoxAsync(this, ResSvc.TryGetString("UnableToOpenFile"), ResSvc.TryGetString("Error"));
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
                    InCaseOfValidLicense(() =>
                    {
                        c = new BahtinovMaskViewModel();
                    });
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
            logger.Info($"Checking for application update");

            string appVersion = AppService.GetAppVersion();

            var (success, result, newVersion) = await AppService.DownloadUrl(appVersion);

            if (success && !string.IsNullOrWhiteSpace(result))
            {
                logger.Info($"Found new version {newVersion}");

                var dialogResult = await DialogService.ShowMessageBoxAsync(null,
                    ResSvc.TryGetString("NewVersionDownload").F(newVersion), ResSvc.TryGetString("NewVersion"), MessageBoxButton.YesNo);

                if (dialogResult is true)
                {
                    AppService.OpenUrl(result);
                }
            }
            else if (!success)
            {
                await DialogService.ShowMessageBoxAsync(null, result, ResSvc.TryGetString("Error"));
            }
        }

        internal void SaveState()
        {
            AppService.SaveState(this);
        }

        internal bool LoadState(string? path = null)
        {
            try
            {
                SettingsViewModel? vm = AppService.LoadState<SettingsViewModel>(path);

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
                    Version = vm.Version ?? AppService.GetAppVersion();
                    GlobalOffsetX = vm.GlobalOffsetX;
                    GlobalOffsetY = vm.GlobalOffsetY;
                    MainWindowOpacity = vm.MainWindowOpacity;

                    SettingsWindowPosition = vm.SettingsWindowPosition;
                    SettingsWindowWidth = vm.SettingsWindowWidth;
                    SettingsWindowHeight = vm.SettingsWindowHeight;
                    ShowKeyboardShortcuts = vm.ShowKeyboardShortcuts;
                    SettingsExpanded = vm.SettingsExpanded;
                    CameraVideoStreamExpanded = vm.CameraVideoStreamExpanded;
                    ProfileManagerExpanded = vm.ProfileManagerExpanded;
                    PinVideoWindowToMainWindow = vm.PinVideoWindowToMainWindow;
                    ShowApplicationLog = vm.ShowApplicationLog;
                    GlobalPropertiesExpanded = vm.GlobalPropertiesExpanded;
                    LastSelectedCamera = vm.LastSelectedCamera;
                    Profiles = vm.Profiles;

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
                InitializeKeyboardShortcuts();
                logger.Info($"Application language changed to '{SelectedLanguage.Value}'");
            }
        }

        partial void OnSelectedThemeChanged(ThemeVariant? oldValue, ThemeVariant newValue)
        {
            Guard.IsNotNull(SelectedTheme, nameof(SelectedTheme));
            Guard.IsNotNull(Application.Current, nameof(Application.Current));
            Guard.IsNotNull(newValue, nameof(newValue));

            if (oldValue == newValue)
            {
                return;
            }

            if (newValue == Themes.Custom.Night)
            {
                InCaseOfValidLicense(() =>
                {
                    Application.Current.RequestedThemeVariant = newValue;
                    logger.Info($"Application theme changed to '{Application.Current.ActualThemeVariant}'");
                });
            }
            else
            {
                Application.Current.RequestedThemeVariant = newValue;
                logger.Info($"Application theme changed to '{Application.Current.ActualThemeVariant}'");
            }
        }

        [RelayCommand]
        internal async Task OpenAboutDialog()
        {
            var dialogViewModel = DialogService.CreateViewModel<AboutViewModel>();

            _ = await DialogService.ShowDialogAsync(this, dialogViewModel);
        }

        [RelayCommand]
        internal static void OpenContactWebPage()
        {
            AppService.OpenUrl(AppService.ContactPage);
        }

        [RelayCommand]
        internal static void OpenGitHubPage()
        {
            AppService.OpenUrl(AppService.GitHubPage);
        }

        [RelayCommand]
        internal static void OpenTwitter()
        {
            AppService.OpenUrl(AppService.TwitterPage);
        }

        [RelayCommand]
        internal static void OpenYouTubeChannel()
        {
            AppService.OpenUrl(AppService.YouTubeChannel);
        }

        [RelayCommand]
        internal static void OpenPatreonWebSite()
        {
            AppService.OpenUrl(AppService.PatreonWebPage);
        }

        [RelayCommand]
        internal static void GitHubIssue()
        {
            AppService.OpenUrl(AppService.GitHubIssue);
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
                case nameof(MainWindowPosition):
                case nameof(MainWindowWidth):
                case nameof(MainWindowHeight):
                case nameof(PinVideoWindowToMainWindow):
                case nameof(ShowApplicationLog):
                case nameof(ShowKeyboardShortcuts):
                case nameof(SelectedLanguage):
                    if (!HasErrors)
                    {
                        base.OnPropertyChanged(e);
                        WeakReferenceMessenger.Default.Send(new SettingsChangedMessage(this));

                        var pVal = Property.GetPropValue(this, e.PropertyName);

                        logger.Debug($"{e.PropertyName} changed to '{pVal}'");
                    }
                    break;
            }
        }
    }
}
