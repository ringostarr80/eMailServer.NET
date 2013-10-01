using System;
using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Driver;

namespace eMailServer {
	public class UserEntity {
		public ObjectId Id { get; set; }
		public string Username { get; set; }
		public string Password { get; set; }
		public string eMail { get; set; }
		public List<eMailAddress> eMailAliases { get; set; }
		public UserStatus Status { get; set; }
		public UserAuthorization Authorization { get; set; }
	}
}

