using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.GridFS;
using SamochodyProject.Model;

namespace SamochodyProject.ViewModel
{
    public partial class UpdateDocument : ObservableObject
    {
        ModelCarsDB ModelDB = new ModelCarsDB();

        private string GetCollectionName()
        {
            return SharedCarProperties.Instance.European ? "SamochodyEuropejskie"
                 : SharedCarProperties.Instance.American ? "SamochodyAmerykanskie"
                 : "SamochodyAzjatyckie";
        }

        [RelayCommand]
        public async Task UpdateDocumentAsync()
        {
            try
            {
                var main = Application.Current?.MainWindow;
                var SPI = SharedCarProperties.Instance;

                if (main == null || !main.Resources.Contains("ReadVM"))
                {
                    MessageBox.Show("Brak kontekstu dostępu do ReadVM.", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (main.Resources["ReadVM"] is not ReadDocument readVm)
                {
                    MessageBox.Show("Nie można uzyskać instancji ReadDocument.", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (SPI.Brand == null || SPI.Model == null || SPI.CarColor == null || SPI.Vintage == null)
                {
                    MessageBox.Show("Jakieś pole własności jest puste! Nie można zaktualizować takiego dokumentu!", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var collectionName = GetCollectionName();
                var collection = ModelDB.DatabaseGetter.GetCollection<BsonDocument>(collectionName);

                FilterDefinition<BsonDocument>? filter = null;
                var selected = readVm.SelectedDocument;

                if (selected != null && selected.Contains("_id"))
                {
                    var idVal = selected.GetValue("_id", BsonNull.Value);
                    if (idVal.IsObjectId) filter = Builders<BsonDocument>.Filter.Eq("_id", idVal.AsObjectId);
                    else if (idVal.IsInt32) filter = Builders<BsonDocument>.Filter.Eq("_id", idVal.AsInt32);
                    else if (idVal.IsInt64) filter = Builders<BsonDocument>.Filter.Eq("_id", idVal.AsInt64);
                    else if (idVal.IsString) filter = Builders<BsonDocument>.Filter.Eq("_id", idVal.AsString);
                    else filter = Builders<BsonDocument>.Filter.Eq("_id", idVal);
                }

                if (filter == null)
                {
                    MessageBox.Show("Nie można utworzyć filtru do aktualizacji/nie wybrałeś dokumentu, którego chcesz zaktualizować.",
                                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var updates = new List<UpdateDefinition<BsonDocument>>();
                var ub = Builders<BsonDocument>.Update;

                updates.Add(ub.Set("marka", BsonValue.Create(SharedCarProperties.Instance.Brand)));
                updates.Add(ub.Set("model", BsonValue.Create(SharedCarProperties.Instance.Model)));
                updates.Add(ub.Set("kolor", BsonValue.Create(SharedCarProperties.Instance.CarColor)));
                updates.Add(ub.Set("rodzaj nadwozia", BsonValue.Create(SharedCarProperties.Instance.BodyKind)));
                updates.Add(ub.Set("rocznik", SharedCarProperties.Instance.Vintage.HasValue ? BsonValue.Create(SharedCarProperties.Instance.Vintage.Value) : BsonNull.Value));

                if (SharedCarProperties.Instance.ImageData != null && SharedCarProperties.Instance.ImageData.Length > 0)
                {
                    try
                    {
                        var bucket = new GridFSBucket(ModelDB.DatabaseGetter);
                        var fileName = !string.IsNullOrWhiteSpace(SharedCarProperties.Instance.ImageFilename) ? System.IO.Path.GetFileName(SharedCarProperties.Instance.ImageFilename) : Guid.NewGuid().ToString();
                        var fileId = await bucket.UploadFromBytesAsync(fileName, SharedCarProperties.Instance.ImageData);
                        updates.Add(ub.Set("imageFileId", new BsonObjectId(fileId)));
                        updates.Add(ub.Set("imageFilename", fileName));
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Nie można zapisać obrazu do GridFS: {ex.Message}", "Błąd zapisu obrazu", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }

                var updateDef = ub.Combine(updates);

                var result = await collection.UpdateOneAsync(filter, updateDef);

                if (result.ModifiedCount > 0 || result.MatchedCount > 0)
                {
                    var updatedDoc = await collection.Find(filter).FirstOrDefaultAsync();
                    if (updatedDoc != null)
                    {
                        if (readVm.Documents != null)
                        {
                            BsonValue? targetId = null;
                            if (updatedDoc.Contains("_id")) targetId = updatedDoc["_id"];

                            if (targetId != null)
                            {
                                var existing = readVm.Documents.FirstOrDefault(d => d.Contains("_id") && d["_id"] == targetId);
                                if (existing != null)
                                {
                                    var idx = readVm.Documents.IndexOf(existing);
                                    if (idx >= 0) readVm.Documents[idx] = updatedDoc;
                                }
                            }
                        }

                        readVm.SelectedDocument = updatedDoc;
                    }

                    MessageBox.Show("Dokument został zaktualizowany.", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Nie zaktualizowano dokumentu (brak dopasowania lub brak zmian).", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas aktualizacji dokumentu: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
