using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Builders;
using MongoDB.Driver.GridFS;
using MongoDB.Driver.Linq;
using NLog;

namespace eMailServer {
	public enum UserAuthorization {
		Normal,
		Administrator
	}

	public enum UserStatus {
		Active,
		Inactive,
		Locked
	}

	public class User {
		public const string COOKIE_USERNAME = "email_username";
		public const string COOKIE_PASSWORD = "email_password";
		private static Logger logger = LogManager.GetCurrentClassLogger();
		private string _id = String.Empty;
		private string _username = String.Empty;
		private string _password = String.Empty;
		private string _eMail = String.Empty;
		private List<eMailAddress> _eMailAliases = new List<eMailAddress>();
		private UserAuthorization _authorization = UserAuthorization.Normal;
		private UserStatus _status = UserStatus.Inactive;
		private MongoServer _mongoServer = null;

		public bool IsLoggedIn {
			get {
				return (this._username != String.Empty && this._password != String.Empty && this._status == UserStatus.Active);
			}
		}

		public string Id { get { return this._id; } }

		public string Username { get { return this._username; } }

		public string Password { get { return this._password; } }

		public string eMail { get { return this._eMail; } }

		public List<eMailAddress> eMailAliases { get { return this._eMailAliases; } }

		public UserAuthorization Authorization { get { return this._authorization; } }

		public UserStatus Status { get { return this._status; } }

		public User() {
			this._mongoServer = MyMongoDB.GetServer();
		}

		public User(string username, string password, UserAuthorization authorization, UserStatus status) {
			this._mongoServer = MyMongoDB.GetServer();

			this._username = username;
			this._password = password;
			this._authorization = authorization;
			this._status = status;
		}

		public User(string username, string password, string eMail) {
			this._mongoServer = MyMongoDB.GetServer();

			this._username = username;
			this._password = password;
			this._eMail = eMail;
		}

		public static bool NameExists(string name) {
			MongoServer mongoServer = MyMongoDB.GetServer();
			MongoDatabase mongoDatabase = mongoServer.GetDatabase("email");
			MongoCollection<UserEntity> mongoCollection = mongoDatabase.GetCollection<UserEntity>("users");
			IMongoQuery queryEMail = Query<UserEntity>.Where(e => e.Username == name);
			UserEntity user = mongoCollection.FindOne(queryEMail);
			if (user != null) {
				return true;
			}

			return false;
		}

		public static bool EMailExists(string eMail) {
			MongoServer mongoServer = MyMongoDB.GetServer();
			MongoDatabase mongoDatabase = mongoServer.GetDatabase("email");
			MongoCollection<UserEntity> mongoCollection = mongoDatabase.GetCollection<UserEntity>("users");
			IMongoQuery queryEMail = Query<UserEntity>.Where(u => u.eMail == eMail || u.eMailAliases.Any(ua => ua.Address == eMail));
			UserEntity user = mongoCollection.FindOne(queryEMail);
			if (user != null) {
				return true;
			}

			return false;
		}

		public static string GetIdByEMail(string eMail) {
			MongoServer mongoServer = MyMongoDB.GetServer();
			MongoDatabase mongoDatabase = mongoServer.GetDatabase("email");
			MongoCollection<UserEntity> mongoCollection = mongoDatabase.GetCollection<UserEntity>("users");
			IMongoQuery queryEMail = Query<UserEntity>.Where(u => u.eMail == eMail || u.eMailAliases.Any(ua => ua.Address == eMail));
			UserEntity user = mongoCollection.FindOne(queryEMail);
			if (user != null) {
				return user.Id.ToString();
			}

			return String.Empty;
		}

		public bool Add() {
			logger.Info("adding User to Database.");

			MongoDatabase mongoDatabase = this._mongoServer.GetDatabase("email");
			MongoCollection mongoCollection = mongoDatabase.GetCollection<UserEntity>("users");

			UserEntity userEntity = new UserEntity {Username = this.Username, Password = this.Password, eMail = this.eMail, Authorization = this.Authorization, Status = this.Status};
			WriteConcernResult result = mongoCollection.Save(userEntity, WriteConcern.Acknowledged);

			logger.Info("WriteConcernResult: " + result.Ok);

			return result.Ok;
		}
		
		public bool RefreshById(string id) {
			MongoDatabase mongoDatabase = this._mongoServer.GetDatabase("email");
			MongoCollection<UserEntity> mongoCollection = mongoDatabase.GetCollection<UserEntity>("users");

			IMongoQuery queryUsername = Query<UserEntity>.Where(u => u.Id == new ObjectId(id));
			UserEntity entityUser = mongoCollection.FindOne(queryUsername);
			if (entityUser != null) {
				this.SetUserByDbResult(entityUser);
				return true;
			}
			
			return false;
		}

		public bool RefreshByCookies(CookieCollection cookies) {
			if (cookies[COOKIE_USERNAME] != null && cookies[COOKIE_USERNAME].Value != String.Empty) {
				if (cookies[COOKIE_PASSWORD] != null && cookies[COOKIE_PASSWORD].Value != String.Empty) {
					MongoDatabase mongoDatabase = this._mongoServer.GetDatabase("email");
					MongoCollection<UserEntity> mongoCollection = mongoDatabase.GetCollection<UserEntity>("users");

					IMongoQuery queryUsername = Query<UserEntity>.Where(e => e.Username == cookies[COOKIE_USERNAME].Value && e.Password == cookies[COOKIE_PASSWORD].Value);
					UserEntity entityUsername = mongoCollection.FindOne(queryUsername);
					if (entityUsername != null) {
						this.SetUserByDbResult(entityUsername);
						return true;
					} else {
						IMongoQuery queryEMail = Query<UserEntity>.Where(e => e.eMail == cookies[COOKIE_USERNAME].Value && e.Password == cookies[COOKIE_PASSWORD].Value);
						UserEntity entityEMail = mongoCollection.FindOne(queryEMail);
						if (entityEMail != null) {
							this.SetUserByDbResult(entityEMail);
							return true;
						}
					}
				}
			}

			return false;
		}

		public bool RefreshByUsernamePassword(string username, string password) {
			if (username != String.Empty && password != String.Empty) {
				MongoDatabase mongoDatabase = this._mongoServer.GetDatabase("email");
				MongoCollection<UserEntity> mongoCollection = mongoDatabase.GetCollection<UserEntity>("users");

				IMongoQuery query = Query<UserEntity>.Where(e => e.Username == username && e.Password == password);
				UserEntity entity = mongoCollection.FindOne(query);
				if (entity != null) {
					this.SetUserByDbResult(entity);
					return true;
				}
			}

			return false;
		}

		public bool RefreshByEMailPassword(string email, string password) {
			if (email != String.Empty && password != String.Empty) {
				MongoDatabase mongoDatabase = this._mongoServer.GetDatabase("email");
				MongoCollection<UserEntity> mongoCollection = mongoDatabase.GetCollection<UserEntity>("users");

				IMongoQuery query = Query<UserEntity>.Where(e => e.eMail == email && e.Password == password);
				UserEntity entity = mongoCollection.FindOne(query);
				if (entity != null) {
					this.SetUserByDbResult(entity);
					return true;
				}
			}

			return false;
		}
		
		private void SetUserByDbResult(UserEntity entity) {
			this._id = entity.Id.ToString();
			this._username = entity.Username;
			this._password = entity.Password;
			this._eMail = entity.eMail;
			this._eMailAliases = entity.eMailAliases;
			this._authorization = entity.Authorization;
			this._status = entity.Status;
		}
		
		public long CountEMails() {
			return this.CountEMails("INBOX");
		}

		public long CountEMails(string directory) {
			MongoDatabase mongoDatabase = this._mongoServer.GetDatabase("email_user_" + this._id);
			MongoCollection<eMailEntity> mongoCollection = mongoDatabase.GetCollection<eMailEntity>("mails");

			//IMongoQuery query = Query<eMailEntity>.Where(e => e.RecipientTo == this.eMail);
			//return mongoCollection.Count(query);
			return mongoCollection.Count();
		}
		
		public List<eMail> GetEmails(int limit) {
			return this.GetEmails(0, limit);
		}

		public List<eMail> GetEmails(int offset, int limit) {
			List<eMail> eMails = new List<eMail>();

			MongoDatabase mongoDatabase = this._mongoServer.GetDatabase("email_user_" + this._id);
			MongoCollection<eMailEntity> mongoCollection = mongoDatabase.GetCollection<eMailEntity>("mails");

			//IMongoQuery query = Query<eMailEntity>.Where(e => e.RecipientTo == this.eMail);
			//MongoCursor<eMailEntity> mongoCursor = mongoCollection.Find(query).SetSkip(offset).SetLimit(limit);
			MongoCursor<eMailEntity> mongoCursor = mongoCollection.FindAll().SetSkip(offset).SetLimit(limit);
			foreach(eMailEntity entity in mongoCursor) {
				eMail mail = new eMail();
				mail.SetId(entity.Id.ToString());
				mail.SetClientName(entity.ClientName);
				mail.SetFrom(entity.MailFrom);
				mail.SetMessage(entity.Message);
				mail.SetRecipient(entity.RecipientTo);
				mail.SetSubject(entity.Subject);
				mail.SetHeaderFrom(entity.HeaderFrom);
				mail.SetHeaderTo(entity.HeaderTo);
				mail.SetTime(entity.Time);
				eMails.Add(mail);
			}

			return eMails;
		}

		public eMail GetEMail(string id) {
			MongoDatabase mongoDatabase = this._mongoServer.GetDatabase("email_user_" + this._id);
			MongoCollection<eMailEntity> mongoCollection = mongoDatabase.GetCollection<eMailEntity>("mails");

			IMongoQuery query = Query<eMailEntity>.Where(e => e.Id == new ObjectId(id));
			eMailEntity entity = mongoCollection.FindOne(query);
			if (entity != null) {
				eMail mail = new eMail();
				mail.SetId(entity.Id.ToString());
				mail.SetClientName(entity.ClientName);
				mail.SetFrom(entity.MailFrom);
				mail.SetMessage(entity.Message);
				mail.SetRecipient(entity.RecipientTo);
				mail.SetSubject(entity.Subject);
				mail.SetHeaderFrom(entity.HeaderFrom);
				mail.SetHeaderTo(entity.HeaderTo);
				mail.SetTime(entity.Time);
				return mail;
			}

			return null;
		}

		public void AddEMail(eMail mail) {
			MongoDatabase mongoDatabase = this._mongoServer.GetDatabase("email_user_" + this._id);
			MongoCollection<eMailEntity> mongoCollection = mongoDatabase.GetCollection<eMailEntity>("mails");

			eMailAddress headerFrom = new eMailAddress(this.Username, this.eMail);
			mail.SetReplyTo(this.Username, this.eMail);

			eMailEntity mailEntity = new eMailEntity {
				Time = mail.Time,
				MailFrom = mail.MailFrom,
				HeaderReplyTo = mail.HeaderReplyTo,
				Subject = mail.Subject,
				RecipientTo = mail.RecipientTo,
				ClientName = "eMailServer.NET",
				Message = mail.Message,
				HeaderFrom = headerFrom,
				RawHeader = mail.RawHeader
			};
			WriteConcernResult result = mongoCollection.Insert(mailEntity, WriteConcern.Acknowledged);

			logger.Info("WriteConcernResult: " + result.Ok);
		}
	}
}
