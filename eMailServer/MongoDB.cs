using System;
using MongoDB.Bson;
using MongoDB.Driver;

namespace eMailServer {
	public class MyMongoDB {
		private const string _connectionString = "mongodb://localhost";

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

