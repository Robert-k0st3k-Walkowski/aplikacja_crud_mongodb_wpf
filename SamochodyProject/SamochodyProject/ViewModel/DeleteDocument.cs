using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SamochodyProject.ViewModel
{
    using CommunityToolkit.Mvvm.ComponentModel;
    using CommunityToolkit.Mvvm.Input;
    using MongoDB.Bson;
    using MongoDB.Driver;
    using System;
    using System.Threading.Tasks;
    using System.Windows;
    using SamochodyProject.Model;

    public partial class DeleteDocument : ObservableObject
    {
        ModelCarsDB ModelDB = new ModelCarsDB();

        [RelayCommand]
        public async Task DeleteSelectedDocumentAsync()
        {
            try
            {
                var main = Application.Current?.MainWindow;

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

                var selected = readVm.SelectedDocument;
                if (selected == null)
                {
                    MessageBox.Show("Brak wybranego dokumentu do usunięcia.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var collectionName = SharedCarProperties.Instance.European ? "SamochodyEuropejskie"
                                   : SharedCarProperties.Instance.American ? "SamochodyAmerykanskie"
                                   : "SamochodyAzjatyckie";

                if (!selected.Contains("_id"))
                {
                    MessageBox.Show("Dokument nie posiada pola _id — nie można usunąć.", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var idVal = selected.GetValue("_id", BsonNull.Value);
                FilterDefinition<BsonDocument> filter;

                if (idVal.IsObjectId)
                {
                    filter = Builders<BsonDocument>.Filter.Eq("_id", idVal.AsObjectId);
                }
                else if (idVal.IsInt32)
                {
                    filter = Builders<BsonDocument>.Filter.Eq("_id", idVal.AsInt32);
                }
                else if (idVal.IsInt64)
                {
                    filter = Builders<BsonDocument>.Filter.Eq("_id", idVal.AsInt64);
                }
                else if (idVal.IsString)
                {
                    filter = Builders<BsonDocument>.Filter.Eq("_id", idVal.AsString);
                }
                else
                {
                    filter = Builders<BsonDocument>.Filter.Eq("_id", idVal);
                }

                var collection = ModelDB.DatabaseGetter.GetCollection<BsonDocument>(collectionName);

                var update = Builders<BsonDocument>.Update.Set("usuniety", true);
                var result = await collection.UpdateOneAsync(filter, update);

                if (result.MatchedCount == 0)
                {
                    MessageBox.Show("Nie znaleziono dokumentu w kolekcji (brak modyfikacji).", "Informacja", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (result.ModifiedCount > 0)
                {
                    if (readVm.Documents != null && readVm.Documents.Contains(selected))
                    {
                        selected["usuniety"] = BsonBoolean.True;
                    }

                    readVm.SelectedDocument = null;

                    MessageBox.Show("Pole 'usuniety' zostało ustawione na 'true' (dokument nadal pozostaje fizycznie w bazie).", "Sukces",
                                    MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Pole 'usuniety' było już ustawione na 'true' lub nie wymagało zmiany.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas ustawiania flagi 'usuniety': {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
