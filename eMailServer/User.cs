using System;
using System.Collections.Generic;
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

		private string _username = String.Empty;
		private string _password = String.Empty;
		private string _eMail = String.Empty;
		private UserAuthorization _authorization = UserAuthorization.Normal;
		private UserStatus _status = UserStatus.Inactive;

		private MongoServer _mongoServer = null;

		public bool IsLoggedIn {
			get {
				return (this._username != String.Empty && this._password != String.Empty && this._status == UserStatus.Active);
			}
		}
		public string Username { get { return this._username; } }
		public string Password { get { return this._password; } }
		public string EMail { get { return this._eMail; } }
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
			return false;
		}

		public static bool EMailExists(string eMail) {
			return false;
		}

		public bool Add() {
			logger.Info("adding User to Database.");

			MongoDatabase mongoDatabase = this._mongoServer.GetDatabase("email");
			MongoCollection mongoCollection = mongoDatabase.GetCollection<UserEntity>("users");

			UserEntity userEntity = new UserEntity {Username = this.Username, Password = this.Password, eMail = this.EMail, Authorization = this.Authorization, Status = this.Status};
			WriteConcernResult result = mongoCollection.Save(userEntity, WriteConcern.Acknowledged);

			logger.Info("WriteConcernResult: " + result.Ok);

			return result.Ok;
		}

		public bool RefreshByCookies(CookieCollection cookies) {
			if (cookies[COOKIE_USERNAME] != null && cookies[COOKIE_USERNAME].Value != String.Empty) {
				if (cookies[COOKIE_PASSWORD] != null && cookies[COOKIE_PASSWORD].Value != String.Empty) {
					MongoDatabase mongoDatabase = this._mongoServer.GetDatabase("email");
					MongoCollection<UserEntity> mongoCollection = mongoDatabase.GetCollection<UserEntity>("users");

					IMongoQuery query = Query<UserEntity>.Where(e => e.Username == cookies[COOKIE_USERNAME].Value && e.Password == cookies[COOKIE_PASSWORD].Value);
					UserEntity entity = mongoCollection.FindOne(query);
					if (entity != null) {
						this._username = entity.Username;
						this._password = entity.Password;
						this._eMail = entity.eMail;
						this._authorization = entity.Authorization;
						this._status = entity.Status;

						return true;
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
					this._username = entity.Username;
					this._password = entity.Password;
					this._eMail = entity.eMail;
					this._authorization = entity.Authorization;
					this._status = entity.Status;

					return true;
				}
			}

			return false;
		}

		public long CountEMails() {
			MongoDatabase mongoDatabase = this._mongoServer.GetDatabase("email");
			MongoCollection<eMailEntity> mongoCollection = mongoDatabase.GetCollection<eMailEntity>("mails");

			IMongoQuery query = Query<eMailEntity>.Where(e => e.RecipientTo == this.EMail);
			return mongoCollection.Count(query);
		}

		public List<eMail> GetEmails(int limit) {
			List<eMail> eMails = new List<eMail>();

			MongoDatabase mongoDatabase = this._mongoServer.GetDatabase("email");
			MongoCollection<eMailEntity> mongoCollection = mongoDatabase.GetCollection<eMailEntity>("mails");

			IMongoQuery query = Query<eMailEntity>.Where(e => e.RecipientTo == this.EMail);
			MongoCursor<eMailEntity> mongoCursor = mongoCollection.Find(query).SetLimit(limit);
			foreach(eMailEntity entity in mongoCursor) {
				eMail mail = new eMail();
				mail.SetClientName(entity.ClientName);
				mail.SetFrom(entity.MailFrom);
				mail.SetMessage(entity.Message);
				if (entity.RecipientTo != String.Empty) {
					mail.SetRecipient(entity.RecipientTo);
				}
				eMails.Add(mail);
			}

			return eMails;
		}

		public void AddEMail(eMail mail) {
			MongoDatabase mongoDatabase = this._mongoServer.GetDatabase("email");
			MongoCollection<eMailEntity> mongoCollection = mongoDatabase.GetCollection<eMailEntity>("mails");

			eMailAddress headerFrom = new eMailAddress(this.Username, this.EMail);
			mail.SetReplyTo(this.Username, this.EMail);

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
