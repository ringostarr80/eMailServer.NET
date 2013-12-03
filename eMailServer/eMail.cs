using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Builders;
using MongoDB.Driver.Linq;
using Bdev.Net.Dns;
using NLog;

namespace eMailServer {
	public class eMail {
		private static Logger logger = LogManager.GetCurrentClassLogger();

		private User _user = new User();
		private string _id = "";
		private string _clientName = String.Empty;
		private DateTime _time = DateTime.Now;
		private string _mailFrom = String.Empty;
		private string _recipientTo = String.Empty;
		private string _subject = String.Empty;
		private string _message = String.Empty;
		private string _folder = String.Empty;
		private eMailAddress _headerFrom = new eMailAddress();
		private eMailAddress _headerReplyTo = null;
		private List<eMailAddress> _headerTo = new List<eMailAddress>();
		private List<eMailAddress> _headerCc = new List<eMailAddress>();
		private DateTime _headerDate;
		private List<string> _flags = new List<string>();
		private List<KeyValuePair<string, string>> _rawHeader = new List<KeyValuePair<string, string>>();
		private MongoServer _mongoServer = null;

		public string Id { get { return this._id; } }

		public string ClientName { get { return this._clientName; } }

		public DateTime Time { get { return this._time; } }

		public string MailFrom { get { return this._mailFrom; } }

		public string MailFromDomain {
			get {
				Match mailDomainMatch = Regex.Match(this._mailFrom, "@(.+)", RegexOptions.Compiled);
				if (mailDomainMatch.Success) {
					return mailDomainMatch.Groups[1].Value;
				}

				return String.Empty;
			}
		}

		public string RecipientTo { get { return this._recipientTo; } }

		public string Subject { get { return this._subject; } }

		public string Message { get { return this._message; } }

		public string Folder { get { return this._folder; } }

		public eMailAddress HeaderFrom { get { return this._headerFrom; } }

		public eMailAddress HeaderReplyTo { get { return this._headerReplyTo; } }

		public List<eMailAddress> HeaderTo { get { return this._headerTo; } }

		public List<eMailAddress> HeaderCc { get { return this._headerCc; } }

		public DateTime HeaderDate { get { return this._headerDate; } }

		public List<string> Flags { get { return this._flags; } }

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
		
		public eMail(eMailEntity entity) {
			this._mongoServer = MyMongoDB.GetServer();
			
			this.SetClientName(entity.ClientName);
			this.SetFrom(entity.MailFrom);
			this.SetHeaderFrom(entity.HeaderFrom);
			this.SetHeaderTo(entity.HeaderTo);
			this.SetId(entity.Id.ToString());
			this.SetMessage(entity.Message);
			this.SetRecipient(entity.RecipientTo);
			this.SetReplyTo(entity.HeaderReplyTo);
			this.SetSubject(entity.Subject);
			this.SetTime(entity.Time);
			this.SetFolder(entity.Folder);
			this._flags = entity.Flags;
		}

		public eMail(LazyBsonDocument bsonDocument) {
			this._mongoServer = MyMongoDB.GetServer();

			this.SetId(bsonDocument["_id"].AsObjectId.ToString());
			if (bsonDocument["ClientName"] != null) {
				this.SetClientName(bsonDocument["ClientName"].AsString);
			}
			if (bsonDocument["MailFrom"] != null) {
				this.SetFrom(bsonDocument["MailFrom"].AsString);
			}
			if (bsonDocument["HeaderFrom"] != null && bsonDocument["HeaderFrom"].IsBsonDocument) {
				BsonDocument bsonHeaderFrom = bsonDocument["HeaderFrom"].AsBsonDocument;
				if (bsonHeaderFrom["Name"] != null && bsonHeaderFrom["Address"] != null) {
					this.SetHeaderFrom(new eMailAddress(bsonHeaderFrom["Name"].AsString, bsonHeaderFrom["Address"].AsString));
				}
			}
			if (bsonDocument["HeaderTo"] != null && bsonDocument["HeaderTo"].IsBsonArray) {
				BsonArray bsonHeaderTo = bsonDocument["HeaderTo"].AsBsonArray;
				List<eMailAddress> headerTo = new List<eMailAddress>();
				foreach(var currentBsonHeaderTo in bsonHeaderTo) {
					if (currentBsonHeaderTo.IsBsonDocument && currentBsonHeaderTo["Name"] != null && currentBsonHeaderTo["Address"] != null) {
						headerTo.Add(new eMailAddress(currentBsonHeaderTo["Name"].AsString, currentBsonHeaderTo["Address"].AsString));
					}
				}
				this.SetHeaderTo(headerTo);
			}
			if (bsonDocument["Message"] != null) {
				this.SetMessage(bsonDocument["Message"].AsString);
			}
			if (bsonDocument["RecipientTo"] != null) {
				this.SetRecipient(bsonDocument["RecipientTo"].AsString);
			}
			if (bsonDocument["HeaderReplyTo"] != null && !bsonDocument["HeaderReplyTo"].IsBsonNull) {
				BsonDocument bsonHeaderReplyTo = bsonDocument["HeaderReplyTo"].AsBsonDocument;
				if (bsonHeaderReplyTo["Name"] != null && bsonHeaderReplyTo["Address"] != null) {
					this.SetReplyTo(bsonHeaderReplyTo["Name"].AsString, bsonHeaderReplyTo["Address"].AsString);
				}
			}
			if (bsonDocument["Subject"] != null) {
				this.SetSubject(bsonDocument["Subject"].AsString);
			}
			if (bsonDocument["Folder"] != null) {
				this.SetFolder(bsonDocument["Folder"].AsString);
			}
			if (bsonDocument["Time"] != null) {
				this.SetTime((DateTime)bsonDocument["Time"].AsBsonDateTime);
			}
			if (bsonDocument["Flags"] != null) {
				//this._flags = bsonDocument["Flags"].AsBsonArray;
			}
		}

		private static IPAddress GetDnsAddress() {
			NetworkInterface[] networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
			foreach(System.Net.NetworkInformation.NetworkInterface networkInterface in networkInterfaces) {
				if (networkInterface.OperationalStatus == OperationalStatus.Up) {
					IPInterfaceProperties ipProperties = networkInterface.GetIPProperties();
					IPAddressCollection dnsAddresses = ipProperties.DnsAddresses;
					foreach(IPAddress dnsAddress in dnsAddresses) {
						return dnsAddress;
					}
				}
			}

			// alternative
			string linuxResolveFilename = "/etc/resolv.conf";
			if (File.Exists(linuxResolveFilename)) {
				string[] lines = File.ReadAllLines(linuxResolveFilename);
				foreach(string line in lines) {
					Match nameserverMatch = Regex.Match(line, "nameserver\\s+([0-9]+)\\.([0-9]+)\\.([0-9]+)\\.([0-9]+)", RegexOptions.Compiled);
					if (nameserverMatch.Success) {
						return IPAddress.Parse(nameserverMatch.Groups[1].Value + "." + nameserverMatch.Groups[2].Value + "." + nameserverMatch.Groups[3].Value + "." + nameserverMatch.Groups[4].Value);
					}
				}
			}

			return null;
		}

		public bool Send() {
			IPAddress dnsServerAddress = GetDnsAddress();
			if (dnsServerAddress == null) {
				logger.Error("cannot find DNS-Server address!");
				return false;
			}

			string mailDomain = String.Empty;
			Match mailDomainMatch = Regex.Match(this.RecipientTo, "@(.+)", RegexOptions.Compiled);
			if (mailDomainMatch.Success) {
				mailDomain = mailDomainMatch.Groups[1].Value;
			} else {
				return false;
			}

			MXRecord[] records = Resolver.MXLookup(mailDomain, dnsServerAddress);
			if (records.Length == 0) {
				return false;
			}

			bool result = false;
			foreach(MXRecord record in records) {
				TcpClient client = new TcpClient(record.DomainName, 587);
				if (!client.Connected) {
					continue;
				}
				SmtpRequestHandler requestHandler = new SmtpRequestHandler(client, eMailServer.Options.SecureSmtpPort, false);
				requestHandler.Connected += (object sender, TcpRequestHandler.TcpRequestEventArgs e) => {
					if (eMailServer.Options.Verbose) {
						logger.Info("Connected to " + e.RemoteEndPoint.Address.ToString() + ":" + e.RemoteEndPoint.Port);
					}
				};
				requestHandler.Disconnected += (object sender, TcpRequestHandler.TcpRequestEventArgs e) => {
					if (eMailServer.Options.Verbose) {
						logger.Info("Disconnected from " + e.RemoteEndPoint.Address.ToString() + ":" + e.RemoteEndPoint.Port);
					}
				};
				requestHandler.CommandReceived += (object sender, SmtpCommandReceivedEventArgs e) => {
					Console.WriteLine("command received => status: " + e.Status + "; message: " + e.Message);
					switch(e.Status) {
						case 220:
							requestHandler.SendMessage(this.MailFromDomain, "EHLO");
							break;
						
						case 250:
							if (e.Message == "STARTTLS") {
								requestHandler.StartTls();
								//if (requestHandler.StartTls()) {
								//Console.WriteLine("send message Go ahead");
								//requestHandler.SendMessage("Go ahead", "220");
								//Console.WriteLine("message Go ahead was sent");
								//}
							}
							requestHandler.SendMessage("MAIL FROM:<" + this.MailFrom + ">");
							break;

						case 530: // Authentication required
							break;

						case 554:
							requestHandler.SendMessage("QUIT");
							requestHandler.Close();
							break;
					}
				};
				requestHandler.Start();
				requestHandler.WaitForClosing();
				result = true;
				break;
			}

			return result;
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

		public void SetClientName(string clientName) {
			if (clientName.Trim() != String.Empty) {
				this._clientName = clientName.Trim();
			}
		}

		public void SetFrom(string mailFrom) {
			string parsedMailAddress = this.ParseMailAddress(mailFrom);
			if (parsedMailAddress != null) {
				this._mailFrom = parsedMailAddress;
			} else {
				throw new FormatException("invalid eMail-address: \"" + mailFrom + "\"");
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

		public void SetUser(User user) {
			this._user = user;
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

		public void SetFolder(string folder) {
			this._folder = folder;
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

			if (eMailAddress.IsValid(mailAddress)) {
				return mailAddress;
			}

			return null;
		}

		private eMailAddress ParseEMailNameAndAddress(string mailAddress) {
			mailAddress = mailAddress.Trim();
			if (mailAddress == String.Empty) {
				return null;
			}

			Match eMailFieldMatch = Regex.Match(mailAddress, "(\"?([^\"]*)\"?)?\\s*<([^>]+)>", RegexOptions.Compiled);
			if (eMailFieldMatch.Success) {
				try {
					return new eMailAddress(eMailFieldMatch.Groups[2].Value.Trim(), eMailFieldMatch.Groups[3].Value.Trim());
				} catch(FormatException) {
					logger.Error("invalid eMail address format: " + eMailFieldMatch.Groups[3].Value);
					return null;
				}
			} else if (eMailAddress.IsValid(mailAddress)) {
				return new eMailAddress(String.Empty, mailAddress);
			}

			return null;
		}

		public bool SaveToMongoDB() {
			if (!this.IsValid) {
				return false;
			}

			logger.Info("Saving received eMail to Database.");

			string userDatabase = "email";
			if (this._user.IsLoggedIn) {
				userDatabase = "email_user_" + this._user.Id;
			} else if (User.EMailExists(this.RecipientTo)) {
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
				Folder = this.Folder,
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
				return result.Ok;
			} catch(Exception e) {
				Console.WriteLine("MongoCollection.Save Exception: " + e.Message);
				return false;
			}
		}
		
		public void AssignToUser(User user) {
			if (this.SaveToMongoDB()) {
				this._user = user;
				MongoDatabase mongoDatabase = this._mongoServer.GetDatabase("email");
				MongoCollection mongoCollection = mongoDatabase.GetCollection<eMailEntity>("mails");
				IMongoQuery query = Query<eMailEntity>.Where(e => e.Id == new ObjectId(this.Id));
				mongoCollection.Remove(query);
			}
		}
	}
}
