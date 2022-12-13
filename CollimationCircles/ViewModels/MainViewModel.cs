using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
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
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace CollimationCircles.ViewModels
{
    public partial class MainViewModel : BaseViewModel
    {
        private readonly IDialogService dialogService;

        [ObservableProperty]
        public PixelPoint position = new(100, 100);

        [ObservableProperty]
        public double width = 650;

        [ObservableProperty]
        public double height = 650;

        [ObservableProperty]
        [Range(0.0, 5.0)]
        public double scale = 1.0;

        [ObservableProperty]
        [Range(0, 360)]
        public double rotation = 0;

        [ObservableProperty]
        public bool showLabels = true;

        [ObservableProperty]
        public ObservableCollection<ICollimationHelper>? items = new();

        [ObservableProperty]
        public ObservableCollection<string> colorList = new();

        public MainViewModel(IDialogService dialogService)
        {
            this.dialogService = dialogService;

            Title = Text.Settings;
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
            List<string> c = new()
            {
                ItemColor.Orange,
                ItemColor.LightBlue,
                ItemColor.LightGreen,
                ItemColor.Yellow,
                ItemColor.Fuchsia,
                ItemColor.Cyan,
                ItemColor.Lime,
                ItemColor.Red,
                ItemColor.Gold,
                ItemColor.White
            };

            ColorList = new ObservableCollection<string>(c);
        }

        private void InitializeDefaults()
        {
            List<ICollimationHelper> list = new()
            {
                // Circles
                new CircleViewModel() { Color = ItemColor.Orange, Radius = 10, Thickness = 1, Label = Text.PrimaryCenter },
                new CircleViewModel() { Color = ItemColor.LightGreen, Radius = 100, Thickness = 2, Label = Text.Inner },
                new CircleViewModel() { Color = ItemColor.LightBlue, Radius = 250, Thickness = 3, Label = Text.PrimaryOuter },

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
            var mainWindow = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop ? desktop.MainWindow : null;
            if (mainWindow != null)
            {
                new SettingsWindow().Show(mainWindow);
            }
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
        private void RemoveItem(ICollimationHelper item)
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
            string jsonString = JsonSerializer.Serialize(Items?.ToList());

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
                    List<ICollimationHelper>? list = JsonSerializer.Deserialize<List<ICollimationHelper>>(content);

                    if (list != null)
                    {
                        Items?.Clear();
                        Items?.AddRange(list);
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
