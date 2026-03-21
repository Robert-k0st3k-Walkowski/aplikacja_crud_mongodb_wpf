using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.GridFS;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;

namespace SamochodyProject.ViewModel
{
    using SamochodyProject.Model;
    using System.Diagnostics;
    using System.Windows.Media.Imaging;

    public partial class ReadDocument : ObservableObject
    {

        ModelCarsDB ModelDB = new ModelCarsDB();

        [ObservableProperty] private ObservableCollection<BsonDocument>? documents;
        [ObservableProperty] private BsonDocument? selectedDocument;
        [ObservableProperty] private string? saveElapsedText;

        partial void OnSelectedDocumentChanged(BsonDocument? value)
        {
            _ = LoadSelectedDocumentAsync();
        }

        private string GetCollectionName()
        {
            return SharedCarProperties.Instance.European ? "SamochodyEuropejskie"
                 : SharedCarProperties.Instance.American ? "SamochodyAmerykanskie"
                 : "SamochodyAzjatyckie";
        }

        [RelayCommand]
        public async Task LoadDocumentsAsync()
        {
            try
            {
                var collectionName = GetCollectionName();
                var collection = ModelDB.DatabaseGetter.GetCollection<BsonDocument>(collectionName);

                var filter = Builders<BsonDocument>.Filter.Or(
                    Builders<BsonDocument>.Filter.Eq("usuniety", false),
                    Builders<BsonDocument>.Filter.Exists("usuniety", false)
                );

                var list = await collection.Find(filter).Limit(100).ToListAsync();
                Documents = new ObservableCollection<BsonDocument>(list);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas ładowania dokumentów: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        public void ShowImage()
        {
            var bytes = SharedCarProperties.Instance.ImageData;
            if (bytes == null || bytes.Length == 0)
            {
                SharedCarProperties.Instance.ImageSource = null;
                return;
            }

            try
            {
                BitmapImage bitmap;
                using (var ms = new MemoryStream(bytes))
                {
                    bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = ms;
                    bitmap.EndInit();
                    bitmap.Freeze();
                }

                SharedCarProperties.Instance.ImageSource = bitmap;
            }
            catch
            {
                SharedCarProperties.Instance.ImageSource = null;
            }
        }

        [RelayCommand]
        public async Task LoadSelectedDocumentAsync()
        {
            try
            {
                if (SelectedDocument == null)
                {
                    SharedCarProperties.Instance.Brand = SharedCarProperties.Instance.Model = SharedCarProperties.Instance.CarColor = null;
                    SharedCarProperties.Instance.BodyKind = SharedCarProperties.Instance.ImageFilename = null;
                    SharedCarProperties.Instance.Vintage = 2000;
                    SharedCarProperties.Instance.ImageData = null;
                    return;
                }

                var doc = SelectedDocument;

                if (doc.Contains("_id"))
                {
                    var idVal = doc["_id"];
                    if (idVal.IsObjectId) SharedCarProperties.Instance.DocumentId = idVal.AsObjectId.ToString();
                    else if (idVal.IsString) SharedCarProperties.Instance.DocumentId = idVal.AsString;
                    else SharedCarProperties.Instance.DocumentId = idVal.ToString();
                }
                else
                {
                    SharedCarProperties.Instance.DocumentId = null;
                }

                SharedCarProperties.Instance.Brand = doc.GetValue("marka", BsonNull.Value).IsBsonNull ? string.Empty : doc.GetValue("marka").ToString();
                SharedCarProperties.Instance.Model = doc.GetValue("model", BsonNull.Value).IsBsonNull ? string.Empty : doc.GetValue("model").ToString();
                SharedCarProperties.Instance.CarColor = doc.GetValue("kolor", BsonNull.Value).IsBsonNull ? string.Empty : doc.GetValue("kolor").ToString();
                SharedCarProperties.Instance.BodyKind = doc.GetValue("rodzaj nadwozia", BsonNull.Value).IsBsonNull ? string.Empty : doc.GetValue("rodzaj nadwozia").ToString();

                var rocznikVal = doc.GetValue("rocznik", BsonNull.Value);
                if (rocznikVal.IsInt32) SharedCarProperties.Instance.Vintage = rocznikVal.AsInt32;
                else if (rocznikVal.IsInt64) SharedCarProperties.Instance.Vintage = (int)rocznikVal.AsInt64;
                else SharedCarProperties.Instance.Vintage = 2000;

                SharedCarProperties.Instance.ImageFilename = doc.GetValue("imageFilename", BsonNull.Value).IsBsonNull ? null : doc.GetValue("imageFilename").ToString();
                SharedCarProperties.Instance.ImageData = null;

                if (doc.Contains("imageFileId"))
                {
                    var fileIdVal = doc["imageFileId"];

                    ObjectId? gridFsId = null;
                    if (fileIdVal.IsObjectId) gridFsId = fileIdVal.AsObjectId;
                    else if (fileIdVal.IsString)
                    {
                        if (ObjectId.TryParse(fileIdVal.AsString, out var parsed)) gridFsId = parsed;
                    }

                    if (gridFsId.HasValue)
                    {
                        try
                        {
                            var bucket = new GridFSBucket(ModelDB.DatabaseGetter);
                            SharedCarProperties.Instance.ImageData = await bucket.DownloadAsBytesAsync(gridFsId.Value);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Nie można pobrać obrazu z GridFS: {ex.Message}", "Błąd pobierania obrazu", MessageBoxButton.OK, MessageBoxImage.Warning);
                            SharedCarProperties.Instance.ImageData = null;
                        }
                    }
                }

                ShowImage();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas wczytywania dokumentu: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        public void ClearSelection()
        {
            SelectedDocument = null;
            SharedCarProperties.Instance.BodyKind = "Kombi";
        }

        private static string GetAvailableFilePath(string directory, string fileName)
        {
            var candidate = Path.Combine(directory, fileName);
            if (!File.Exists(candidate)) return candidate;

            var name = Path.GetFileNameWithoutExtension(fileName);
            var ext = Path.GetExtension(fileName);
            var i = 1;
            string next;
            do
            {
                next = Path.Combine(directory, $"{name}_{i}{ext}");
                i++;
            } while (File.Exists(next));

            return next;
        }

        private static string GetDesktopFolderPath()
        {
            try
            {
                var path = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                if (!string.IsNullOrEmpty(path)) return path;
            }
            catch
            {
            
            }

            return AppContext.BaseDirectory;
        }

        [RelayCommand]
        public async Task SaveDocumentToFile()
        {
            if (SelectedDocument == null)
            {
                MessageBox.Show("Brak wybranego dokumentu do zapisania.", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            SaveElapsedText = null;

            var defaultName = SelectedDocument.Contains("_id") ? SelectedDocument["_id"].ToString() : "document";

            var totalWatch = Stopwatch.StartNew();
            double docMs = 0;
            double imageMs = 0;

            string? docPath = null;
            string? imagePath = null;

            try
            {
                var desktopFolder = GetDesktopFolderPath();

                var json = SelectedDocument.ToJson(new MongoDB.Bson.IO.JsonWriterSettings { Indent = true });
                var docFileName = defaultName + ".json";
                docPath = GetAvailableFilePath(desktopFolder, docFileName);

                var docWatch = Stopwatch.StartNew();
                File.WriteAllText(docPath, json);
                docWatch.Stop();
                docMs = docWatch.Elapsed.TotalMilliseconds;
            }
            catch (Exception ex)
            {
                totalWatch.Stop();
                SaveElapsedText = $"Błąd zapisu dokumentu: {totalWatch.Elapsed.TotalMilliseconds:F2} ms";
                MessageBox.Show($"Nie można zapisać pliku: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                byte[]? imageBytes = SharedCarProperties.Instance.ImageData;

                if ((imageBytes == null || imageBytes.Length == 0) && SelectedDocument.Contains("imageFileId"))
                {
                    var fileIdVal = SelectedDocument["imageFileId"];
                    ObjectId? gridFsId = null;
                    if (fileIdVal.IsObjectId) gridFsId = fileIdVal.AsObjectId;
                    else if (fileIdVal.IsString)
                    {
                        if (ObjectId.TryParse(fileIdVal.AsString, out var parsed)) gridFsId = parsed;
                    }

                    if (gridFsId.HasValue)
                    {
                        try
                        {
                            var bucket = new GridFSBucket(ModelDB.DatabaseGetter);
                            var downloadWatch = Stopwatch.StartNew();
                            imageBytes = await bucket.DownloadAsBytesAsync(gridFsId.Value);
                            downloadWatch.Stop();
                            imageMs += downloadWatch.Elapsed.TotalMilliseconds;
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Nie można pobrać obrazu z GridFS: {ex.Message}", "Błąd pobierania obrazu", MessageBoxButton.OK, MessageBoxImage.Warning);
                            imageBytes = null;
                        }
                    }
                }

                if (imageBytes == null || imageBytes.Length == 0)
                {
                    totalWatch.Stop();
                    var totalMs = totalWatch.Elapsed.TotalMilliseconds;
                    var different = totalMs - (docMs + 0.0);
                    SaveElapsedText = $"Czas zapisu — Dokument: {docMs:F2} ms; Obraz: {0:F2} ms; Inne: {different:F2} ms; Razem: {totalMs:F2} ms";

                    var infoMsg = $"Dokument zapisany poprawnie:{Environment.NewLine}{docPath}{Environment.NewLine}{Environment.NewLine}{SaveElapsedText}";
                    MessageBox.Show(infoMsg, "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var desktopForImage = GetDesktopFolderPath();
                var imageFilename = SharedCarProperties.Instance.ImageFilename;
                var detectedExt = GetImageExtensionFromBytes(imageBytes);
                var defaultImageName = !string.IsNullOrWhiteSpace(imageFilename) ? imageFilename : (defaultName + detectedExt);
                imagePath = GetAvailableFilePath(desktopForImage, defaultImageName);

                var imgWatch = Stopwatch.StartNew();
                File.WriteAllBytes(imagePath, imageBytes);
                imgWatch.Stop();
                imageMs += imgWatch.Elapsed.TotalMilliseconds;
            }
            catch (Exception ex)
            {
                totalWatch.Stop();
                SaveElapsedText = $"Błąd zapisu obrazu: {totalWatch.Elapsed.TotalMilliseconds:F2} ms";
                MessageBox.Show($"Błąd podczas zapisywania obrazu: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            totalWatch.Stop();
            var total = totalWatch.Elapsed.TotalMilliseconds;
            var otherOperations = total - (docMs + imageMs);
            SaveElapsedText = $"Czas zapisu — Dokument: {docMs:F2} ms; Obraz: {imageMs:F2} ms; Pozostałe operacje: {otherOperations:F2} ms; Razem: {total:F2} ms";

            var finalMsg = $"Dokument zapisany poprawnie:{Environment.NewLine}{docPath}{Environment.NewLine}";

            if (!string.IsNullOrEmpty(imagePath))
            {
                finalMsg += $"{Environment.NewLine}Obraz zapisany poprawnie:{Environment.NewLine}{imagePath}{Environment.NewLine}";
            }

            MessageBox.Show(finalMsg, "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private static string GetImageExtensionFromBytes(byte[] data)
        {
            if (data == null || data.Length < 4) return ".jpg";

            if (data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47) return ".png";
            if (data[0] == 0xFF && data[1] == 0xD8) return ".jpg";
            if (data[0] == 0x47 && data[1] == 0x49 && data[2] == 0x46) return ".gif";
            if (data[0] == 0x42 && data[1] == 0x4D) return ".bmp";
            if (data.Length >= 12 && data[8] == 0x57 && data[9] == 0x45 && data[10] == 0x42 && data[11] == 0x50) return ".webp";

            return ".jpg";
        }
    }
}
