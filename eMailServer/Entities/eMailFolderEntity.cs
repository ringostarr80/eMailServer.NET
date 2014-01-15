using System;
using MongoDB.Bson;
using MongoDB.Driver;

namespace eMailServer {
	public class eMailFolderEntity {
		public ObjectId Id { get; set; }
		public string Name { get; set; }
	}
}

