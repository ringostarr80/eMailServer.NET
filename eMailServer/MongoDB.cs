using System;
using MongoDB.Bson;
using MongoDB.Driver;

namespace eMailServer {
	public class MyMongoDB {
		private static string _connectionString = "mongodb://" + eMailServer.Options.DatabaseAddress;

		private static MongoServer _mongoServer = null;

		public MyMongoDB() {
			MongoClient mongoClient = new MongoClient(_connectionString);
			_mongoServer = mongoClient.GetServer();
		}

		public static MongoServer GetServer() {
			if (_mongoServer == null) {
				MongoClient mongoClient = new MongoClient(_connectionString);
				_mongoServer = mongoClient.GetServer();
			}

			return _mongoServer;
		}
	}
}

