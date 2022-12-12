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
        public double width = 600;

        [ObservableProperty]
        public double height = 600;

        [ObservableProperty]
        [Range(0.0, 5.0)]
        public double scale = 1.0;

        [ObservableProperty]
        public bool showLabels = false;

        [ObservableProperty]
        public ObservableCollection<ItemViewModel> items = new();

        [ObservableProperty]
        public ObservableCollection<string> colorList = new();

        [ObservableProperty]
        public ObservableCollection<string> typeList = new();

        public MainViewModel(IDialogService dialogService)
        {
            this.dialogService = dialogService;

            Title = Text.Settings;
            InitializeColors();
            InitializeTypes();
            InitializeDefaults();
            InitializeMessages();
        }

        private void InitializeMessages()
        {
            WeakReferenceMessenger.Default.Register<ItemChangedMessage>(this, (r, m) =>
            {
                var item = items.SingleOrDefault(x => x.Id == m.Value.id);
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
                ItemColor.Tomato,
                ItemColor.Gold,
                ItemColor.White
            };

            colorList = new ObservableCollection<string>(c);
        }

        private void InitializeTypes()
        {
            List<string> c = new()
            {
                ItemType.Circle,
                ItemType.Cross,
                ItemType.Screw
            };

            typeList = new ObservableCollection<string>(c);
        }

        private void InitializeDefaults()
        {
            List<ItemViewModel> list = new()
            {
                // Circles
                new() { Color = ItemColor.Orange, Radius = 10, Thickness = 1, Type = ItemType.Circle, Label = $"{Text.Circle} 1" },
                new() { Color = ItemColor.LightGreen, Radius = 50, Thickness = 2, Type= ItemType.Circle, Label = $"{Text.Circle} 2" },
                new() { Color = ItemColor.LightBlue, Radius = 100, Thickness = 3, Type = ItemType.Circle, Label = $"{Text.Circle} 3" },
                new() { Color = ItemColor.Yellow, Radius = 200, Thickness = 4, Type = ItemType.Circle, Label = $"{Text.Circle} 4" },
                new() { Color = ItemColor.Fuchsia, Radius = 300, Thickness = 5, Type = ItemType.Circle, Label = $"{Text.Circle} 5" },

                // Crosses
                new() { Color = ItemColor.Cyan, Radius = 300, Thickness = 2, Type = ItemType.Cross, Label = $"{Text.Cross} 1" },

                // Screws
                new() { Color = ItemColor.Cyan, Radius = 300, Thickness = 2, Type = ItemType.Screw, Label = $"{Text.Screw} 1" }
            };

            items.Clear();
            items.AddRange(list);

            items.CollectionChanged += Items_CollectionChanged;
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
            new SettingsWindow().Show(mainWindow);
        }

        [RelayCommand]
        private void AddCircle()
        {
            items.Add(new ItemViewModel() { Label = $"{Text.Circle}" });
        }

        [RelayCommand]
        private void RemoveItem(ItemViewModel item)
        {
            items.Remove(item);
        }

        [RelayCommand]
        private void ResetList()
        {
            InitializeDefaults();
        }

        [RelayCommand]
        private async Task SaveList()
        {
            string jsonString = JsonSerializer.Serialize(items.ToList());

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

            if (!string.IsNullOrWhiteSpace(result))
            {
                File.WriteAllText(result, jsonString);
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

            if (result != null)
            {
                string content = File.ReadAllText(result);

                if (!string.IsNullOrWhiteSpace(content))
                {
                    List<ItemViewModel>? list = JsonSerializer.Deserialize<List<ItemViewModel>>(content);

                    if (list != null)
                    {
                        items.Clear();
                        items.AddRange(list);
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
