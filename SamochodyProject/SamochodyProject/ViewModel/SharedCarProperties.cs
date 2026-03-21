using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace SamochodyProject.ViewModel
{
    public partial class SharedCarProperties : ObservableObject
    {
        public static SharedCarProperties Instance { get; } = new SharedCarProperties();

        private SharedCarProperties() { }

        [ObservableProperty] private string? brand;
        [ObservableProperty] private string? model;
        [ObservableProperty] private int? vintage = 2000;
        [ObservableProperty] private string? carColor;
        [ObservableProperty] private string? bodyKind;

        [ObservableProperty] private bool european = true;
        [ObservableProperty] private bool american;
        [ObservableProperty] private bool asian;

        [ObservableProperty] private byte[]? imageData;
        [ObservableProperty] private string? imageFilename;
        [ObservableProperty] private string? imageUploadSuccess = "Brak dodanego obrazu";
        [ObservableProperty] private ImageSource? imageSource;

        [ObservableProperty] private string? documentId;
        [ObservableProperty] private bool deleted = false;
    }
}
