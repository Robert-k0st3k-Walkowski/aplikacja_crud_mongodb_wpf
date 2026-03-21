using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.GridFS;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace SamochodyProject.ViewModel
{
    using SamochodyProject.Model;

    public partial class CreateDocument : ObservableObject
    {
        ModelCarsDB ModelDB = new ModelCarsDB();

        [RelayCommand]
        public void SelectImage()
        {
            var dlg = new OpenFileDialog
            {
                Title = "Wybierz obraz samochodu",
                Filter = "Image files|*.jpg;*.jpeg;*.png;*.bmp;*.gif"
            };

            if (dlg.ShowDialog() == true)
            {
                SharedCarProperties.Instance.ImageFilename = dlg.FileName;
                var fileNameOnly = Path.GetFileName(SharedCarProperties.Instance.ImageFilename);

                try
                {
                    SharedCarProperties.Instance.ImageData = File.ReadAllBytes(SharedCarProperties.Instance.ImageFilename);
                    SharedCarProperties.Instance.ImageUploadSuccess = $"Dodano: {fileNameOnly}";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Nie można odczytać obrazu: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    SharedCarProperties.Instance.ImageFilename = null;
                    SharedCarProperties.Instance.ImageData = null;
                }
            }
        }

        [RelayCommand]
        public async Task GenerateDocument()
        {
            try
            {
                var collectionName = SharedCarProperties.Instance.European ? "SamochodyEuropejskie"
                                 : SharedCarProperties.Instance.American ? "SamochodyAmerykanskie"
                                 : "SamochodyAzjatyckie";

                var car = new BsonDocument
                {
                    { "marka", BsonValue.Create(SharedCarProperties.Instance.Brand) },
                    { "model", BsonValue.Create(SharedCarProperties.Instance.Model) },
                    { "rodzaj nadwozia", BsonValue.Create(SharedCarProperties.Instance.BodyKind) },
                    { "kolor", BsonValue.Create(SharedCarProperties.Instance.CarColor) },
                    { "rocznik", BsonValue.Create(SharedCarProperties.Instance.Vintage) },
                    { "usuniety", BsonValue.Create(SharedCarProperties.Instance.Deleted) }
                };

                if (SharedCarProperties.Instance.ImageData != null && SharedCarProperties.Instance.ImageData.Length > 0)
                {
                    var bucket = new GridFSBucket(ModelDB.DatabaseGetter);
                    var fileId = await bucket.UploadFromBytesAsync(Path.GetFileName(SharedCarProperties.Instance.ImageFilename) ?? Guid.NewGuid().ToString(), SharedCarProperties.Instance.ImageData);
                    car.Add("imageFileId", new BsonObjectId(fileId));
                    car.Add("imageFilename", Path.GetFileName(SharedCarProperties.Instance.ImageFilename) ?? BsonNull.Value.ToString());
                }

                var collection = ModelDB.DatabaseGetter.GetCollection<BsonDocument>(collectionName);
                await collection.InsertOneAsync(car);

                MessageBox.Show($"Dodano samochód do kolekcji '{collectionName}'.", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);

                SharedCarProperties.Instance.Brand = string.Empty;
                SharedCarProperties.Instance.Model = string.Empty;
                SharedCarProperties.Instance.CarColor = string.Empty;
                SharedCarProperties.Instance.BodyKind = "Kombi";
                SharedCarProperties.Instance.ImageData = null;
                SharedCarProperties.Instance.ImageFilename = null;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas dodawania dokumentu: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        [RelayCommand]
        public void ResetProperties()
        {
            SharedCarProperties.Instance.Brand = null;
            SharedCarProperties.Instance.Model = null;
            SharedCarProperties.Instance.CarColor = null;
            SharedCarProperties.Instance.Vintage = 2000;
            SharedCarProperties.Instance.BodyKind = "Kombi";
            SharedCarProperties.Instance.ImageSource = null;
            SharedCarProperties.Instance.ImageData = null;
            SharedCarProperties.Instance.ImageUploadSuccess = "Brak dodanego obrazu";
            SharedCarProperties.Instance.European = true;
            SharedCarProperties.Instance.American = false;
            SharedCarProperties.Instance.Asian = false;
        }
    }
}
