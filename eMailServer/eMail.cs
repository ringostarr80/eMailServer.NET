using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using MongoDB.Bson;
using MongoDB.Driver;

namespace eMailServer {
	public class eMail {
		private string _clientName = String.Empty;
		private string _from = String.Empty;
		private List<string> _recipients = new List<string>();
		private string _message = String.Empty;

		public string ClientName { get { return this._clientName; } }
		public string From { get { return this._from; } }
		public List<string> Recipients { get { return this._recipients; } }
		public string Message { get { return this._message; } }

		public bool IsValid {
			get {
				if (this._from != String.Empty && this._recipients.Count > 0) {
					return true;
				}
				return false;
			}
		}

		public eMail() {

		}

		public void SetClientName(string clientName) {
			if (clientName.Trim() != String.Empty) {
				this._clientName = clientName.Trim();
			}
		}

		public void SetFrom(string mailFrom) {
			mailFrom = mailFrom.Trim();
			if (mailFrom == String.Empty) {
				return;
			}


		}

		public void SaveToMongoDB() {
			if (!this.IsValid) {
				return;
			}

			string connectionString = "mongodb://localhost";
			MongoClient mongoClient = new MongoClient(connectionString);
			MongoServer mongoServer = mongoClient.GetServer();
			MongoDatabase mongoDatabase = mongoServer.GetDatabase("email");
		}
	}
}

