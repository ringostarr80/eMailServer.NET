using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using MongoDB.Bson;
using MongoDB.Driver;
using NLog;

namespace eMailServer {
	public class eMail {
		private static Logger logger = LogManager.GetCurrentClassLogger();

		private string _clientName = String.Empty;
		private string _from = String.Empty;
		private List<string> _recipients = new List<string>();
		private string _subject = String.Empty;
		private List<string> _header = new List<string>();
		private string _message = String.Empty;

		private MongoServer _mongoServer = null;

		public string ClientName { get { return this._clientName; } }
		public string From { get { return this._from; } }
		public List<string> Recipients { get { return this._recipients; } }
		public string Subject { get { return this._subject; } }
		public List<string> Header { get { return this._header; } }
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
			this._mongoServer = MyMongoDB.GetServer();
		}

		public void Send() {

		}

		public void SetClientName(string clientName) {
			if (clientName.Trim() != String.Empty) {
				this._clientName = clientName.Trim();
			}
		}

		public void SetFrom(string mailFrom) {
			string parsedMailAddress = this.ParseMailAddress(mailFrom);
			if (parsedMailAddress != null) {
				this._from = parsedMailAddress;
			}
		}

		public void SetRecipient(string mailRecipient) {
			string parsedMailAddress = this.ParseMailAddress(mailRecipient);
			if (parsedMailAddress != null) {
				this._recipients.Add(parsedMailAddress);
			}
		}

		public void SetSubject(string subject) {
			this._subject = subject;
		}

		public void ParseData(string data) {
			bool header = true;

			this._header = new List<string>();
			List<string> messageLines = new List<string>();

			string[] lines = data.Split('\n');
			foreach(string line in lines) {
				string trimmedLine = line.Trim();
				if (header && trimmedLine == String.Empty) {
					header = false;
					continue;
				}
				if (trimmedLine == String.Empty) {
					continue;
				}

				if (header) {
					this._header.Add(trimmedLine);
					this.ParseHeaderLine(trimmedLine);
				} else {
					messageLines.Add(trimmedLine);
				}
			}

			this._message = String.Join("\r\n", messageLines);
		}

		private void ParseHeaderLine(string line) {
			Console.WriteLine("ParseHeaderLine(" + line + ")");
			Match subjectMatch = Regex.Match(line, "^Subject:(.*)", RegexOptions.IgnoreCase);
			if (subjectMatch.Success) {
				this._subject = subjectMatch.Groups[1].Value.Trim();
				Console.WriteLine("Subject found: " + this._subject);
			}
		}

		public void SetMessage(string message) {
			this._message = message;
		}

		private string ParseMailAddress(string mailAddress) {
			mailAddress = mailAddress.Trim();
			if (mailAddress == String.Empty) {
				return null;
			}

			Match eMailFieldMatch = Regex.Match(mailAddress, "<([^>]+)>", RegexOptions.Compiled);
			if (eMailFieldMatch.Success) {
				mailAddress = mailAddress.Trim(new char[] {'<', '>'});
			}

			Match eMailMatch = Regex.Match(mailAddress.Trim(), "^([^@]+@[^\\.]+\\.[a-zA-Z]+)$", RegexOptions.Compiled);
			if (eMailMatch.Success) {
				return eMailMatch.Groups[1].Value;
			}

			return null;
		}

		public void SaveToMongoDB() {
			if (!this.IsValid) {
				return;
			}

			logger.Info("Saving received eMail to Database.");

			MongoDatabase mongoDatabase = this._mongoServer.GetDatabase("email");
			MongoCollection mongoCollection = mongoDatabase.GetCollection<eMailEntity>("mails");

			eMailEntity mailEntity = new eMailEntity {ClientName = this.ClientName, From = this.From, Subject = this.Subject, Recipients = this.Recipients, Header = this.Header, Message = this.Message};
			WriteConcernResult result = mongoCollection.Save(mailEntity, WriteConcern.Acknowledged);

			logger.Info("WriteConcernResult: " + result.Ok);
		}
	}
}

