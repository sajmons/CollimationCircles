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
        public ObservableCollection<MarkViewModel> marks = new();

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
            WeakReferenceMessenger.Default.Register<CircleChangedMessage>(this, (r, m) =>
            {
                var item = marks.SingleOrDefault(x => x.Id == m.Value.id);
                if (item != null)
                {
                    item.Thickness = m.Value.Thickness;
                    item.Color = m.Value.Color;
                    item.Radius = m.Value.Radius;
                    item.IsCross = m.Value.IsCross;
                    item.Spacing = m.Value.Spacing;
                    item.Label = m.Value.Label;
                    item.Rotation = m.Value.Rotation;
                }

                WeakReferenceMessenger.Default.Send(new SettingsChangedMessage(this));
            });
        }

        private void InitializeColors()
        {
            List<string> c = new()
            {
                CColor.Orange,
                CColor.LightBlue,
                CColor.LightGreen,
                CColor.Yellow,
                CColor.Fuchsia,
                CColor.Cyan,
                CColor.Lime,
                CColor.Tomato,
                CColor.Gold,
                CColor.White
            };

            colorList = new ObservableCollection<string>(c);
        }

        private void InitializeDefaults()
        {
            List<MarkViewModel> list = new()
            {
                // Circles
                new() { Color = CColor.Orange, Radius = 10, Thickness = 1, Label = $"{Text.Circle} 1" },
                new() { Color = CColor.LightGreen, Radius = 50, Thickness = 2, Label = $"{Text.Circle} 2" },
                new() { Color = CColor.LightBlue, Radius = 100, Thickness = 3, Label = $"{Text.Circle} 3" },
                new() { Color = CColor.Yellow, Radius = 200, Thickness = 4, Label = $"{Text.Circle} 4" },
                new() { Color = CColor.Fuchsia, Radius = 300, Thickness = 5, Label = $"{Text.Circle} 5" },

                // Crosses
                new() { Color = CColor.Cyan, Radius = 300, Thickness = 2, IsCross = true, Label = $"{Text.Cross} 1" }
            };

            marks.Clear();
            marks.AddRange(list);

            marks.CollectionChanged += Circles_CollectionChanged;
        }

        private void Croses_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            WeakReferenceMessenger.Default.Send(new SettingsChangedMessage(this));
        }

        private void Circles_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
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
            new SettingsWindow().ShowDialog(mainWindow);
        }

        [RelayCommand]
        private void AddCircle()
        {
            marks.Add(new MarkViewModel() { Label = $"{Text.Circle} {Marks.Count}" });
        }

        [RelayCommand]
        private void RemoveCircle(MarkViewModel circle)
        {
            marks.Remove(circle);
        }

        [RelayCommand]
        private void ResetList()
        {
            InitializeDefaults();
        }

        [RelayCommand]
        private async Task SaveList()
        {
            string jsonString = JsonSerializer.Serialize(marks.ToList());

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
                    List<MarkViewModel>? list = JsonSerializer.Deserialize<List<MarkViewModel>>(content);

                    if (list != null)
                    {
                        marks.Clear();
                        marks.AddRange(list);
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
