using System;
using System.Collections.Generic;
using System.Globalization;
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
		private List<KeyValuePair<string, string>> _header = new List<KeyValuePair<string, string>>();
		private string _message = String.Empty;

		private MongoServer _mongoServer = null;

		public string ClientName { get { return this._clientName; } }
		public string From { get { return this._from; } }
		public List<string> Recipients { get { return this._recipients; } }
		public string Subject { get { return this._subject; } }
		public List<KeyValuePair<string, string>> Header { get { return this._header; } }
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

			this._header = new List<KeyValuePair<string, string>>();
			List<string> messageLines = new List<string>();

			string[] lines = data.Split('\n');
			KeyValuePair<string, string> lastHeader = new KeyValuePair<string, string>("", "");
			foreach(string line in lines) {
				string trimmedLine = line.Trim();
				if (header && trimmedLine == String.Empty) {
					header = false;
					if (lastHeader.Key != String.Empty) {
						this._header.Add(lastHeader);
						lastHeader = new KeyValuePair<string, string>("", "");
					}
					continue;
				}
				if (trimmedLine == String.Empty) {
					continue;
				}

				if (header) {
					KeyValuePair<string, string> currentHeader = this.ParseHeaderLine(trimmedLine);
					if (currentHeader.Key == String.Empty && currentHeader.Value != String.Empty) {
						if (lastHeader.Key != String.Empty) {
							lastHeader = new KeyValuePair<string, string>(lastHeader.Key, lastHeader.Value + "\r\n" + currentHeader.Value);
						}
					} else if (currentHeader.Key != String.Empty) {
						if (lastHeader.Key != String.Empty) {
							this._header.Add(lastHeader);
						}
						lastHeader = currentHeader;
					}
				} else {
					messageLines.Add(trimmedLine);
				}
			}

			this._message = String.Join("\r\n", messageLines);
		}

		private KeyValuePair<string, string> ParseHeaderLine(string line) {
			KeyValuePair<string, string> header = new KeyValuePair<string, string>("", "");

			Console.WriteLine("ParseHeaderLine(" + line + ")");
			Match headerMatch = Regex.Match(line, @"^([^:\s]+):(.*)", RegexOptions.IgnoreCase);
			if (headerMatch.Success) {
				string headerKey = headerMatch.Groups[1].Value.Trim().ToUpper();
				string headerValue = headerMatch.Groups[2].Value.Trim();
				switch(headerKey) {
					case "DATE":
						header = new KeyValuePair<string, string>(headerMatch.Groups[1].Value.Trim(), headerValue);
						Console.WriteLine("Date found: " + headerValue);
						break;

					case "FROM":
						header = new KeyValuePair<string, string>(headerMatch.Groups[1].Value.Trim(), headerValue);
						Console.WriteLine("From found: " + headerValue);
						break;

					case "TO":
						header = new KeyValuePair<string, string>(headerMatch.Groups[1].Value.Trim(), headerValue);
						Console.WriteLine("To found: " + headerValue);
						break;

					case "SUBJECT":
						header = new KeyValuePair<string, string>(headerMatch.Groups[1].Value.Trim(), headerValue);
						this._subject = headerValue;
						//Console.WriteLine("Subject found: " + this._subject);
						break;

					default:
						header = new KeyValuePair<string, string>(headerMatch.Groups[1].Value.Trim(), headerValue);
						break;
				}
			} else {
				header = new KeyValuePair<string, string>("", line.Trim());
			}

			return header;
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
				mailAddress = mailAddress.Trim(new char[] {'<', '>'}).Trim();
			}

			RegexUtilities regexUtility = new RegexUtilities();
			if (regexUtility.IsValidEmail(mailAddress)) {
				return mailAddress;
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

