using CollimationCircles.Messages;
using CollimationCircles.Resources.Strings;
using CollimationCircles.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using HanumanInstitute.MvvmDialogs;
using HanumanInstitute.MvvmDialogs.FrameworkDialogs;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
        private static SettingsWindow? settingsWindow;

        [ObservableProperty]
        public double width = 600;

        [ObservableProperty]
        public double height = 600;

        [ObservableProperty]
        public ObservableCollection<MarkViewModel> marks = new();

        [ObservableProperty]
        public ObservableCollection<string> colors = new();
        
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
                }

                WeakReferenceMessenger.Default.Send(new SettingsChangedMessage(this));
            });
        }

        private void InitializeColors()
        {
            List<string> c = new()
            {
                "Red",
                "Blue",
                "Green",
                "Yellow",
                "Magenta",
                "Cyan"
            };

            colors = new ObservableCollection<string>(c);
        }

        private void InitializeDefaults()
        {
            List<MarkViewModel> list = new()
            {
                new() { Color = "Red", Radius = 10, Thickness = 1 },
                new() { Color = "Green", Radius = 50, Thickness = 2 },
                new() { Color = "Blue", Radius = 100, Thickness = 3 },
                new() { Color = "Yellow", Radius = 200, Thickness = 4 },
                new() { Color = "Magenta", Radius = 300, Thickness = 5 },

                new() { Color = "Cyan", Radius = 300, Thickness = 2, IsCross = true }
            };

            marks = new ObservableCollection<MarkViewModel>(list);

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
            //WeakReferenceMessenger.Default.Send(new SettingsChangedMessage(this));
        }

        [RelayCommand]
        private void SettingsButton()
        {
            if (settingsWindow == null)
            {
                settingsWindow = new SettingsWindow();
                settingsWindow.Show();
            }
            else if(!settingsWindow.IsVisible)
            {
                settingsWindow.Show();
            }
        }

        [RelayCommand]
        private void AddCircle()
        {
            marks.Add(new MarkViewModel());
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
                Title = "Save file",
                InitialDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!,
                Filters = new List<FileFilter>()
                {
                    new FileFilter("JSON Documents", new[] { "json" }),
                },
                DefaultExtension = "json"
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
                Title = "Open file",
                InitialDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!,
                Filters = new List<FileFilter>()
                {
                    new FileFilter(
                        "JSON Documents",
                        new[]
                        {
                            "json"
                        })
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
                        marks = new ObservableCollection<MarkViewModel>(list);
                        marks.CollectionChanged += Circles_CollectionChanged;
                    }
                    else
                    {
                        await dialogService.ShowMessageBoxAsync(this, "Unable to open file.");
                    }
                }
            }
        }
    }
}
