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
        [Range(0, 360)]
        public double rotation = 0;

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

            Title = Text.CollimationCircles;
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
                new CircleViewModel() { ItemColor = Colors.Orange, Radius = 10, Thickness = 1, Label = Text.PrimaryCenter },
                new CircleViewModel() { ItemColor = Colors.LightGreen, Radius = 100, Thickness = 2, Label = Text.Inner },
                new CircleViewModel() { ItemColor = Colors.LightBlue, Radius = 250, Thickness = 3, Label = Text.PrimaryOuter },

                // Crosses
                new CrossViewModel(),

                // Screws
                new ScrewViewModel()
            };

            if (Items is not null)
            {
                Items.CollectionChanged += Items_CollectionChanged;

                Items.Clear();
                Items.AddRange(list);
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
        private void ChangeColor()
        { 
            //Color = newColor
        }

        [RelayCommand]
        private async Task SaveList()
        {
            string jsonString = JsonConvert.SerializeObject(this);

            var settings = new SaveFileDialogSettings
            {
                Title = Text.SaveFile,
                InitialDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!,
                Filters = new List<FileFilter>()
                {
                    new FileFilter(Text.JSONDocuments, new[] { Text.Json }),
                },
                DefaultExtension = Text.Json
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
                InitialDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!,
                Filters = new List<FileFilter>()
                {
                    new FileFilter(Text.JSONDocuments, new[] { Text.Json })
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
                        Scale= vm.Scale;
                        Rotation= vm.Rotation;
                        ShowLabels= vm.ShowLabels;
                        ColorList= vm.ColorList;

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
