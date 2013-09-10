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

		private string _id = "";
		private string _clientName = String.Empty;
		private DateTime _time = DateTime.Now;
		private string _mailFrom = String.Empty;
		private string _recipientTo = String.Empty;
		private string _subject = String.Empty;
		private string _message = String.Empty;
		private eMailAddress _headerFrom = new eMailAddress();
		private eMailAddress _headerReplyTo = null;
		private List<eMailAddress> _headerTo = new List<eMailAddress>();
		private List<eMailAddress> _headerCc = new List<eMailAddress>();
		private DateTime _headerDate;
		private List<KeyValuePair<string, string>> _rawHeader = new List<KeyValuePair<string, string>>();

		private MongoServer _mongoServer = null;

		public string Id { get { return this._id; } }
		public string ClientName { get { return this._clientName; } }
		public DateTime Time { get { return this._time; } }
		public string MailFrom { get { return this._mailFrom; } }
		public string RecipientTo { get { return this._recipientTo; } }
		public string Subject { get { return this._subject; } }
		public string Message { get { return this._message; } }
		public eMailAddress HeaderFrom { get { return this._headerFrom; } }
		public eMailAddress HeaderReplyTo { get { return this._headerReplyTo; } }
		public List<eMailAddress> HeaderTo { get { return this._headerTo; } }
		public List<eMailAddress> HeaderCc { get { return this._headerCc; } }
		public DateTime HeaderDate { get { return this._headerDate; } }
		public List<KeyValuePair<string, string>> RawHeader { get { return this._rawHeader; } }

		public bool IsValid {
			get {
				if (this._mailFrom != String.Empty && this._recipientTo != String.Empty) {
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
				this._mailFrom = parsedMailAddress;
			}
		}

		public void SetRecipient(string mailRecipient) {
			string parsedMailAddress = this.ParseMailAddress(mailRecipient);
			if (parsedMailAddress != null) {
				this._recipientTo = parsedMailAddress;
			}
		}

		public void SetSubject(string subject) {
			this._subject = subject;
		}

		public void ParseData(string data) {
			bool header = true;

			this._rawHeader = new List<KeyValuePair<string, string>>();
			List<string> messageLines = new List<string>();

			string[] lines = data.Split('\n');
			KeyValuePair<string, string> lastHeader = new KeyValuePair<string, string>("", "");
			foreach(string line in lines) {
				string trimmedLine = line.Trim();
				if (header && trimmedLine == String.Empty) {
					header = false;
					if (lastHeader.Key != String.Empty) {
						this._rawHeader.Add(lastHeader);
						lastHeader = new KeyValuePair<string, string>("", "");
					}
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
							this._rawHeader.Add(lastHeader);
						}
						lastHeader = currentHeader;
					}
				} else {
					if (trimmedLine == "..") {
						trimmedLine = ".";
					}
					messageLines.Add(trimmedLine);
				}
			}

			foreach(KeyValuePair<string, string> currentHeader in this._rawHeader) {
				switch(currentHeader.Key.ToUpper()) {
					case "CC":
						string[] headerCcs = currentHeader.Value.Split('\n');
						foreach(string headerCc in headerCcs) {
							eMailAddress nameAndAddressCc = this.ParseEMailNameAndAddress(headerCc);
							if (nameAndAddressCc != null) {
								this._headerCc.Add(nameAndAddressCc);
							}
						}
						break;

					case "DATE":
						try {
							string timezoneCleanedDate = Regex.Replace(currentHeader.Value.Trim(), @"\s+\((CEST|GMT|UTC)\)$", "", RegexOptions.Compiled);
							this._headerDate = DateTime.Parse(timezoneCleanedDate);
						} catch(Exception e) {
							logger.ErrorException("error while parsing the eMail header date: " + currentHeader.Value.Trim(), e);
						}
						break;

					case "FROM":
						eMailAddress nameAndAddressFrom = this.ParseEMailNameAndAddress(currentHeader.Value);
						if (nameAndAddressFrom != null) {
							this._headerFrom = nameAndAddressFrom;
						}
						break;

					case "REPLY-TO":
						eMailAddress nameAndAddressReplyTo = this.ParseEMailNameAndAddress(currentHeader.Value);
						if (nameAndAddressReplyTo != null) {
							this._headerReplyTo = nameAndAddressReplyTo;
						}
						break;

					case "SUBJECT":
						this._subject = currentHeader.Value.Trim();
						break;

					case "TO":
						string[] headerTos = currentHeader.Value.Split('\n');
						foreach(string headerTo in headerTos) {
							eMailAddress nameAndAddressTo = this.ParseEMailNameAndAddress(headerTo);
							if (nameAndAddressTo != null) {
								this._headerTo.Add(nameAndAddressTo);
							}
						}
						break;
				}
			}

			this._message = String.Join("\r\n", messageLines);
		}

		private KeyValuePair<string, string> ParseHeaderLine(string line) {
			KeyValuePair<string, string> header = new KeyValuePair<string, string>("", "");

			Match headerMatch = Regex.Match(line, @"^([^:\s]+):(.*)", RegexOptions.IgnoreCase);
			if (headerMatch.Success) {
				string headerValue = headerMatch.Groups[2].Value.Trim();
				header = new KeyValuePair<string, string>(headerMatch.Groups[1].Value.Trim(), headerValue);
			} else {
				header = new KeyValuePair<string, string>("", line.Trim());
			}

			return header;
		}

		public void SetId(string id) {
			this._id = id;
		}

		public void SetMessage(string message) {
			this._message = message;
		}

		public void SetReplyTo(eMailAddress mailAddress) {
			this._headerReplyTo = mailAddress;
		}

		public void SetReplyTo(string name, string address) {
			this._headerReplyTo = new eMailAddress(name, address);
		}

		public void SetHeaderFrom(eMailAddress headerFrom) {
			this._headerFrom = headerFrom;
		}

		public void SetHeaderTo(List<eMailAddress> headerTo) {
			this._headerTo = headerTo;
		}

		public void SetTime(DateTime time) {
			this._time = time;
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

		private eMailAddress ParseEMailNameAndAddress(string mailAddress) {
			mailAddress = mailAddress.Trim();
			if (mailAddress == String.Empty) {
				return null;
			}

			Match eMailFieldMatch = Regex.Match(mailAddress, "(\"([^\"]*)\")?\\s*<([^>]+)>", RegexOptions.Compiled);
			if (eMailFieldMatch.Success) {
				try {
					return new eMailAddress(eMailFieldMatch.Groups[2].Value, eMailFieldMatch.Groups[3].Value);
				} catch(FormatException) {
					logger.Error("invalid eMail address format: " + eMailFieldMatch.Groups[3].Value);
					return null;
				}
			}

			return null;
		}

		public void SaveToMongoDB() {
			if (!this.IsValid) {
				return;
			}

			logger.Info("Saving received eMail to Database.");

			string userDatabase = "email";
			if (User.EMailExists(this.RecipientTo)) {
				string userId = User.GetIdByEMail(this.RecipientTo);
				if (userId != String.Empty) {
					userDatabase = "email_user_" + userId;
				}
			}

			MongoDatabase mongoDatabase = this._mongoServer.GetDatabase(userDatabase);
			MongoCollection mongoCollection = mongoDatabase.GetCollection<eMailEntity>("mails");

			eMailEntity mailEntity = new eMailEntity {
				ClientName = this.ClientName,
				Time = this.Time,
				MailFrom = this.MailFrom,
				Subject = this.Subject,
				RecipientTo = this.RecipientTo,
				Message = this.Message,
				HeaderFrom = this.HeaderFrom,
				HeaderTo = this.HeaderTo,
				HeaderDate = this.HeaderDate,
				RawHeader = this.RawHeader
			};

			if (this.HeaderCc.Count > 0) {
				mailEntity.HeaderCc = this.HeaderCc;
			}
			if (this.HeaderReplyTo != null && this.HeaderReplyTo.Address != String.Empty) {
				mailEntity.HeaderReplyTo = this.HeaderReplyTo;
			}

			try {
				WriteConcernResult result = mongoCollection.Save(mailEntity, WriteConcern.Acknowledged);
				logger.Info("WriteConcernResult: " + result.Ok);
			} catch(Exception e) {
				Console.WriteLine("MongoCollection.Save Exception: " + e.Message);
			}
		}
	}
}
