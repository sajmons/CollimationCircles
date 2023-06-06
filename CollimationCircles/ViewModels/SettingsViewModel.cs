using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
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
        private INotifyPropertyChanged? dialogViewModel;

        [ObservableProperty]
        private INotifyPropertyChanged? aboutDialogViewModelHandler;

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
        [Range(0, 4)]
        [NotifyDataErrorInfo]
        public double scale = 1.0;

        [JsonProperty]
        [ObservableProperty]
        public double labelSize = 10;

        [JsonProperty]
        [ObservableProperty]
        [Range(-180, 180)]
        [NotifyDataErrorInfo]
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
        public int selectedIndex = 0;

        [ObservableProperty]
        public ObservableCollection<KeyValuePair<string, string>> languageList = new();

        [JsonProperty]
        [ObservableProperty]
        public KeyValuePair<string, string> selectedLanguage = new();

        [JsonProperty]
        [ObservableProperty]
        public bool checkForNewVersionOnStartup = true;

        [JsonProperty]
        [ObservableProperty]
        public string version = string.Empty;        

        [ObservableProperty]
        public string appDescription;

        public SettingsViewModel(IDialogService dialogService, IAppService appService)
        {
            this.dialogService = dialogService;
            this.appService = appService;

            if (this.appService is not null)
            {
                InitializeColors();
                InitializeDefaults();
                InitializeMessages();
            }

            Title = $"{DynRes.TryGetString("CollimationCircles")} - {DynRes.TryGetString("Settings")} - {DynRes.TryGetString("Version")} {appService?.GetAppVersion()}";
            MainTitle = $"{DynRes.TryGetString("CollimationCircles")} - {DynRes.TryGetString("Version")} {appService?.GetAppVersion()}";
            AppDescription = $"{DynRes.TryGetString("AppDescription")}\n{DynRes.TryGetString("Copyright")} {DynRes.TryGetString("Author")}";
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
            // initialize languages
            List<KeyValuePair<string, string>> l = new()
            {
                new KeyValuePair<string, string>("English", "en-US"),
                new KeyValuePair<string, string>("Slovenian", "sl-SI"),
                new KeyValuePair<string, string>("German", "de-DE")
            };

            LanguageList = new ObservableCollection<KeyValuePair<string, string>>(l);
            SelectedLanguage = LanguageList.FirstOrDefault();

            List<CollimationHelper> list = new()
                {
                    // Circles
                    new CircleViewModel() { ItemColor = Colors.LightGreen, Radius = 100, Thickness = 2, Label = DynRes.TryGetString("Inner") },
                    new CircleViewModel() { ItemColor = Colors.LightBlue, Radius = 250, Thickness = 3, Label = DynRes.TryGetString("PrimaryOuter") },

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

            Items.CollectionChanged += Items_CollectionChanged;

            SelectedIndex = 0;

            RotationAngle = 0;
            Scale = 1;
            ShowLabels = true;

            Version = appService?.GetAppVersion() ?? "0.0.0";
        }

        private void Items_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            WeakReferenceMessenger.Default.Send(new SettingsChangedMessage(this));
        }

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            if (!HasErrors)
            {
                base.OnPropertyChanged(e);
                WeakReferenceMessenger.Default.Send(new SettingsChangedMessage(this));
            }
        }

        [RelayCommand]
        internal void ShowSettings()
        {
            if (DialogViewModel is null)
            {
                DialogViewModel = dialogService?.CreateViewModel<SettingsViewModel>();

                if (DialogViewModel is not null)
                {
                    dialogService?.Show(null, DialogViewModel);
                }
            }
        }

        [RelayCommand]
        internal void CloseSettings()
        {
            if (DialogViewModel is not null)
            {
                dialogService?.Close(DialogViewModel);
                DialogViewModel = null;
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
                if (!LoadState(path?.Path?.LocalPath))
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
            DialogViewModel = null;
            AboutDialogViewModelHandler = null;
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
                    Position = vm.Position;
                    Width = vm.Width;
                    Height = vm.Height;
                    Scale = vm.Scale;
                    RotationAngle = vm.RotationAngle;
                    ShowLabels = vm.ShowLabels;
                    ColorList = vm.ColorList;
                    LabelSize = vm.LabelSize;

                    Items.Clear();
                    Items.AddRange(vm.Items);

                    SelectedLanguage = vm.SelectedLanguage;
                    CheckForNewVersionOnStartup = vm.CheckForNewVersionOnStartup;
                    Version = vm.Version ?? appService?.GetAppVersion() ?? "0.0.0";
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

        partial void OnSelectedLanguageChanged(KeyValuePair<string, string> value)
        {
            if (SelectedLanguage.Value is not null)
            {
                Translate(SelectedLanguage.Value);
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
    }
}
