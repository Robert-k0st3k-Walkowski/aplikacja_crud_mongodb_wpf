using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SamochodyProject.Model
{
    public class ModelCarsDB
    {
        private MongoClient Client;
        private IMongoDatabase DataBase;
        private List<IMongoCollection<BsonDocument>> Collections;

        public ModelCarsDB()
        {
            // Należy dodać "connection string" do loklanej bazy MongoDB, z taką samą nazwą oraz kolekcjami
            Client = new MongoClient("");
            DataBase = Client.GetDatabase("Samochody");

            Collections = new List<IMongoCollection<BsonDocument>>();

            Collections.Add(DataBase.GetCollection<BsonDocument>("SamochodyEuropejskie"));
            Collections.Add(DataBase.GetCollection<BsonDocument>("SamochodyAmerykanskie"));
            Collections.Add(DataBase.GetCollection<BsonDocument>("SamochodyAzjatyckie"));
        }

        public IMongoDatabase DatabaseGetter
        {
            get
            {
                return this.DataBase;
            }
        }

        public IMongoCollection<BsonDocument> this[int index]
        {
            get
            {
                return Collections[index];
            }
        }
    }
}
