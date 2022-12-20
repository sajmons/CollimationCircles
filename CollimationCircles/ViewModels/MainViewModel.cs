using Avalonia;
using Avalonia.Media;
using CollimationCircles.Messages;
using CollimationCircles.Models;
using CollimationCircles.Resources.Strings;
using CollimationCircles.Views;
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
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace CollimationCircles.ViewModels
{
    [JsonObject(MemberSerialization.OptIn)]
    public partial class MainViewModel : BaseViewModel
    {
        private readonly IDialogService dialogService;

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
        [Range(0.0, 5.0)]
        public double scale = 1.0;

        [JsonProperty]
        [ObservableProperty]
        [Range(-180, 180)]
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

        public MainViewModel(IDialogService dialogService)
        {
            this.dialogService = dialogService;
            
            InitializeColors();
            InitializeDefaults();
            InitializeMessages();
        }

        private void InitializeMessages()
        {
            WeakReferenceMessenger.Default.Register<ItemChangedMessage>(this, (r, m) =>
            {
                var item = Items?.SingleOrDefault(x => x.Id == m.Value.Id);

                if (item != null)
                {
                    item = m.Value;
                }

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
            List<CollimationHelper> list = new()
            {
                // Circles
                new CircleViewModel() { ItemColor = Colors.LightGreen, Radius = 100, Thickness = 2, Label = Text.Inner },
                new CircleViewModel() { ItemColor = Colors.LightBlue, Radius = 250, Thickness = 3, Label = Text.PrimaryOuter },

                // Crosses
                new CrossViewModel(),

                // Screws
                new ScrewViewModel(),

                // Primarey clip
                new PrimaryClipViewModel(),

                // Spider
                new SpiderViewModel()
            };

            if (Items is not null)
            {
                Items.Clear();
                Items.AddRange(list);

                Items.CollectionChanged += Items_CollectionChanged;
            }
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
        private void SettingsButton()
        {
            new SettingsWindow().Show();
        }

        [RelayCommand]
        private void AddCircle()
        {
            Items?.Add(new CircleViewModel());
        }

        [RelayCommand]
        private void AddCross()
        {
            Items?.Add(new CrossViewModel());
        }

        [RelayCommand]
        private void AddScrew()
        {
            Items?.Add(new ScrewViewModel());
        }

        [RelayCommand]
        private void AddClip()
        {
            Items?.Add(new PrimaryClipViewModel());
        }

        [RelayCommand]
        private void AddSpider()
        {
            Items?.Add(new SpiderViewModel());
        }

        [RelayCommand]
        private void RemoveItem(CollimationHelper item)
        {
            Items?.Remove(item);
        }

        [RelayCommand]
        private void ResetList()
        {
            InitializeDefaults();
        }

        [RelayCommand]
        private async Task SaveList()
        {
            string jsonString = JsonConvert.SerializeObject(this, new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Auto
            });

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
                File.WriteAllText(path, jsonString, System.Text.Encoding.UTF8);
            }
        }

        [RelayCommand]
        private async Task LoadList()
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
                string content = File.ReadAllText(path, System.Text.Encoding.UTF8);

                if (!string.IsNullOrWhiteSpace(content))
                {
                    var jss = new JsonSerializerSettings
                    {
                        TypeNameHandling = TypeNameHandling.Auto,
                        NullValueHandling = NullValueHandling.Ignore,
                    };

                    MainViewModel? vm = JsonConvert.DeserializeObject<MainViewModel>(content, jss);

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
                    }
                    else
                    {
                        await dialogService.ShowMessageBoxAsync(this, Text.UnableToOpenFile);
                    }
                }
            }
        }
    }
}
