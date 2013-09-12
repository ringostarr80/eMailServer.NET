using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using NLog;

namespace eMailServer {
	public class HttpRequestHandler {
		private static Logger logger = LogManager.GetCurrentClassLogger();

		private User _user = new User();
		private XmlDocument _doc;
		private HttpListenerResponse _response = null;
		private HttpListenerRequest _request = null;
		private string _xmlRoot = "email_server";

		private string _defaultAdminUserName = "admin";
		private string _defaultAdminUserPassword = "email";

		public User User { get { return this._user; } }
		public string DefaultAdminUserName { get { return this._defaultAdminUserName; } }
		public string DefaultAdminUserPassword { get { return this._defaultAdminUserPassword; } }
		public string RequestUrl { get; set; }
		public string RequestRawUrl { get; set; }
		public NameValueCollection RequestQueryString { get; set; }
		public NameValueCollection RequestHeaders { get; set; }
		public Stream ResponseOutputStream { get; set; }
		public HttpListenerResponse Response {
			get {
				return this._response;
			}
			set {
				this._response = value;
				this.ResponseOutputStream = this._response.OutputStream;
			}
		}
		/// <summary>
		/// Gets or sets the request.
		/// </summary>
		/// <value>
		/// The request.
		/// </value>
		public HttpListenerRequest Request {
			get {
				return this._request;
			}
			set {
				this._request = value;
				this.RequestUrl = this._request.Url.ToString();
				this.RequestRawUrl = this._request.RawUrl;
				this.RequestQueryString = this._request.QueryString;
				this.RequestHeaders = this._request.Headers;
			}
		}

		public HttpRequestHandler() {
			this._doc = new XmlDocument();
			this._doc.CreateXmlDeclaration("1.0", "utf-8", "yes");
			this._doc.AppendChild(this._doc.CreateElement(this._xmlRoot));
		}

		public HttpRequestHandler(HttpListenerContext context) {
			this.Request = context.Request;
			this.Response = context.Response;

			this._doc = new XmlDocument();
			this._doc.CreateXmlDeclaration("1.0", "utf-8", "yes");
			this._doc.AppendChild(this._doc.CreateElement(this._xmlRoot));

			this.RefreshUser();
		}

		private void RefreshUser() {
			if (!this.User.RefreshByCookies(this.Request.Cookies)) {
				if (this.Request.Cookies[User.COOKIE_USERNAME] != null && this.Request.Cookies[User.COOKIE_USERNAME].Value == this.DefaultAdminUserName) {
					if (this.Request.Cookies[User.COOKIE_PASSWORD] != null && this.Request.Cookies[User.COOKIE_PASSWORD].Value == this.DefaultAdminUserPassword) {
						this._user = new User(this.Request.Cookies[User.COOKIE_USERNAME].Value, this.Request.Cookies[User.COOKIE_PASSWORD].Value, UserAuthorization.Administrator, UserStatus.Active);
					}
				}
			}
		}

		public void ProcessRequest() {
			string RequestType = "";
			
			logger.Trace("Url: " + this.RequestUrl);
			XmlNode XmlRoot = this._doc.GetElementsByTagName(this._xmlRoot).Item(0);
			
			XmlElement XmlRequest = this._doc.CreateElement("request");
			string UrlPath = Regex.Replace(this.RequestRawUrl, "[^\\/]*$", "");
			Match TypeMatch = Regex.Match(UrlPath, "^\\/([^\\/]+)\\/");
			if (TypeMatch.Success) {
				RequestType = TypeMatch.Groups[1].Value;
			}
			XmlRequest.SetAttribute("type", RequestType);
			XmlRequest.SetAttribute("url", this.RequestUrl);
			XmlRequest.SetAttribute("url_path", UrlPath);
			XmlElement XmlParams = this._doc.CreateElement("params");
			for(int i = 0; i < this.RequestQueryString.Count; i++) {
				XmlElement XmlParam = this._doc.CreateElement("param");
				XmlParam.SetAttribute("name", this.RequestQueryString.GetKey(i));
				XmlParam.SetAttribute("value", this.RequestQueryString[i].ToString());
				XmlParams.AppendChild(XmlParam);
			}
			XmlRequest.AppendChild(XmlParams);
			XmlRoot.AppendChild(XmlRequest);
			
			bool accessAllowed = false;
			if (!accessAllowed) {
				if (this.Request.Url.Host == "localhost" || this.Request.Url.Host == "127.0.0.1") {
					accessAllowed = true;
				} else {
					/*
					Options options = eMailServer.Options;
					if (options.AllowedRemoteAddresses != String.Empty) {
						string remoteAddress = this.Request.RemoteEndPoint.Address.ToString();
						string[] allowedRemoteAddresses = options.AllowedRemoteAddresses.Split(',');
						foreach(string allowedRemoteAddress in allowedRemoteAddresses) {
							if (allowedRemoteAddress == remoteAddress) {
								accessAllowed = true;
								break;
							} else if (allowedRemoteAddress.Contains("*")) {
								Match allowedRemoteAddressWildcardMatch = Regex.Match(allowedRemoteAddress, "^([0-9]+|\\*)\\.([0-9]+|\\*)\\.([0-9]+|\\*)\\.([0-9]+|\\*)$", RegexOptions.Compiled);
								if (allowedRemoteAddressWildcardMatch.Success) {
									Match remoteAddressMatch = Regex.Match(remoteAddress, "^([0-9]+)\\.([0-9]+)\\.([0-9]+)\\.([0-9]+)$", RegexOptions.Compiled);
									if (remoteAddressMatch.Success) {
										if ((allowedRemoteAddressWildcardMatch.Groups[1].Value != "*" && remoteAddressMatch.Groups[1].Value == allowedRemoteAddressWildcardMatch.Groups[1].Value || allowedRemoteAddressWildcardMatch.Groups[1].Value == "*") &&
											(allowedRemoteAddressWildcardMatch.Groups[2].Value != "*" && remoteAddressMatch.Groups[2].Value == allowedRemoteAddressWildcardMatch.Groups[2].Value || allowedRemoteAddressWildcardMatch.Groups[2].Value == "*") &&
											(allowedRemoteAddressWildcardMatch.Groups[3].Value != "*" && remoteAddressMatch.Groups[3].Value == allowedRemoteAddressWildcardMatch.Groups[3].Value || allowedRemoteAddressWildcardMatch.Groups[3].Value == "*") &&
											(allowedRemoteAddressWildcardMatch.Groups[4].Value != "*" && remoteAddressMatch.Groups[4].Value == allowedRemoteAddressWildcardMatch.Groups[4].Value || allowedRemoteAddressWildcardMatch.Groups[4].Value == "*")) {
											accessAllowed = true;
										}
									}
								}
							}
						}
						//accessAllowed = allowedRemoteAddresses.Any(a => a == remoteAddress);
					}
					*/
				}
			}
			accessAllowed = true;
			
			if (accessAllowed) {
				try {
					switch(RequestType) {
						case "files":
							this.OutputFile(this.RequestRawUrl);
							break;
	
						case "login":
							this.OutputFile("/files/login.html");
							break;
	
						case "logout":
							this.OutputFile("/files/logout.html");
							break;
						
						case "mail":
							this.Mail(this.RequestRawUrl, this.RequestQueryString);
							break;
	
						case "mails":
							this.Mails(this.RequestRawUrl, this.RequestQueryString);
							break;
	
						case "register":
							this.OutputFile("/files/register.html");
							break;
	
						case "":
							this.OutputFile("/files/index.html");
							break;
	
						default:
							//Console.WriteLine("Error: Invalid RequestType = " + RequestType);
							break;
					}
				} catch(UnauthorizedAccessException) {
					this.Redirect("/");
					return;
				}
			} else {
				XmlElement XmlError = this._doc.CreateElement("error");
				XmlError.SetAttribute("code", ((int)HttpStatusCode.Unauthorized).ToString());
				XmlError.SetAttribute("message", "access denied");
				XmlError.SetAttribute("remote_address", this.Request.RemoteEndPoint.Address.ToString());
				XmlRoot.AppendChild(XmlError);
			}
		}

		private void Mail(string rawUrl, NameValueCollection queryString) {
			if (!this.User.IsLoggedIn) {
				throw new UnauthorizedAccessException("access denied");
			}

			XmlNode XmlRoot = this._doc.GetElementsByTagName(this._xmlRoot).Item(0);
			XmlElement XmlMail = this._doc.CreateElement("mail");

			rawUrl = Regex.Replace(rawUrl.Replace("/mail/", ""), "\\?.*", "", RegexOptions.Compiled);
			switch(rawUrl) {
				case "get":
					if (queryString["id"] != null && queryString["id"] != String.Empty) {
						eMail mail = this.User.GetEMail(queryString["id"]);
						if (mail != null) {
							XmlMail.SetAttribute("from", mail.MailFrom);
							XmlMail.SetAttribute("to", mail.RecipientTo);
							XmlMail.SetAttribute("subject", mail.Subject);

							XmlElement XmlRecipients = this._doc.CreateElement("recipients");
							XmlMail.AppendChild(XmlRecipients);

							XmlElement XmlMessage = this._doc.CreateElement("message");
							XmlMessage.InnerText = mail.Message;
							XmlMail.AppendChild(XmlMessage);
						}
					}
					break;

				case "write":
					if (this.Request.HttpMethod == "POST") {
						using(HttpPostRequest.HttpPostRequest postRequest = new HttpPostRequest.HttpPostRequest(this.Request)) {
							string toEMail = String.Empty;
							string subject = String.Empty;
							string message = String.Empty;

							if (postRequest.Parameters["email"] != null) {
								toEMail = postRequest.Parameters["email"];
							}
							if (postRequest.Parameters["subject"] != null) {
								subject = postRequest.Parameters["subject"];
							}
							if (postRequest.Parameters["message"] != null) {
								message = postRequest.Parameters["message"];
							}

							eMail newEMail = new eMail();
							newEMail.SetFrom(this.User.EMail);
							newEMail.SetRecipient(toEMail);
							newEMail.SetSubject(subject);
							newEMail.SetMessage(message);

							XmlMail.SetAttribute("from", newEMail.MailFrom);
							XmlMail.SetAttribute("to", newEMail.RecipientTo);
							XmlMail.SetAttribute("subject", newEMail.Subject);

							XmlElement XmlRecipients = this._doc.CreateElement("recipients");
							/*
							foreach(string recipient in newEMail.Recipients) {
								XmlElement XmlRecipient = this._doc.CreateElement("recipient");
								XmlRecipient.SetAttribute("email", recipient);
								XmlRecipients.AppendChild(XmlRecipient);
							}
							*/
							XmlMail.AppendChild(XmlRecipients);

							XmlElement XmlMessage = this._doc.CreateElement("message");
							XmlMessage.InnerText = newEMail.Message;
							XmlMail.AppendChild(XmlMessage);

							this.User.AddEMail(newEMail);
							newEMail.Send();
						}
					}
					break;
			}

			XmlRoot.AppendChild(XmlMail);
		}

		private void Mails(string rawUrl, NameValueCollection queryString) {
			if (!this.User.IsLoggedIn) {
				throw new UnauthorizedAccessException("access denied");
			}

			XmlNode XmlRoot = this._doc.GetElementsByTagName(this._xmlRoot).Item(0);
			XmlElement XmlMails = this._doc.CreateElement("mails");

			rawUrl = Regex.Replace(rawUrl.Replace("/mails/", ""), "\\?.*", "", RegexOptions.Compiled);
			switch(rawUrl) {
				case "all":
					int mailLimit = -1;
					if (queryString["limit"] != null && queryString["limit"] != String.Empty) {

					}
					List<eMail> eMails = this.User.GetEmails(mailLimit);
					foreach(eMail mail in eMails) {
						XmlElement XmlMail = this._doc.CreateElement("mail");
						XmlMail.SetAttribute("id", mail.Id);
						XmlMail.SetAttribute("from", mail.MailFrom);
						XmlMail.SetAttribute("to", mail.RecipientTo);
						XmlMail.SetAttribute("subject", mail.Subject);
						XmlMail.SetAttribute("time", mail.Time.ToString("yyyy-MM-dd HH:mm:ss"));
						XmlMail.SetAttribute("client_name", mail.ClientName);
						XmlElement XmlMessage = this._doc.CreateElement("message");
						XmlMessage.InnerText = mail.Message;
						XmlMail.AppendChild(XmlMessage);

						XmlElement XmlHeaderFrom = this._doc.CreateElement("header_from");
						XmlHeaderFrom.SetAttribute("name", mail.HeaderFrom.Name);
						XmlHeaderFrom.SetAttribute("address", mail.HeaderFrom.Address);
						XmlMail.AppendChild(XmlHeaderFrom);

						XmlElement XmlRecipients = this._doc.CreateElement("recipients");
						foreach(eMailAddress recipient in mail.HeaderTo) {
							XmlElement XmlRecipient = this._doc.CreateElement("recipient");
							XmlRecipient.SetAttribute("name", recipient.Name);
							XmlRecipient.SetAttribute("address", recipient.Address);
							XmlRecipients.AppendChild(XmlRecipient);
						}
						XmlMail.AppendChild(XmlRecipients);
						XmlMails.AppendChild(XmlMail);
					}
					break;

				case "count":
					long eMailsCount = this.User.CountEMails();
					XmlMails.SetAttribute("count", eMailsCount.ToString());
					break;
			}

			XmlRoot.AppendChild(XmlMails);
		}

		private void OutputFile(string file) {
			logger.Trace("OutputFile(" + file + ")");

			// Sonderbedingungen
			if (this.Route(file)) {
				return;
			}

			// Normale Ausgabe von Dateien
			this.Response.AddHeader("Last-Modified", eMailServer.StartTime.ToString("R"));
			
			string IfNoneMatch = "";
			if (this.RequestHeaders["If-None-Match"] != null && this.RequestHeaders["If-None-Match"] != "") {
				IfNoneMatch = this.RequestHeaders["If-None-Match"];
			}
			
			NameValueCollection FileEtags = eMailServer.FileEtags;
			if (FileEtags[file] != null && FileEtags[file] != "" && "\"" + FileEtags[file] + "\"" == IfNoneMatch) {
				this.Response.StatusCode = (int)HttpStatusCode.NotModified;
				this.Response.AddHeader("Etag", "\"" + FileEtags[file] + "\"");
				this.ResponseOutputStream.Close();
			} else {
				Assembly assembly = Assembly.GetExecutingAssembly();
				try {
					Stream stream = assembly.GetManifestResourceStream("eMailServer" + file.Replace("/", "."));
					
					if (stream.CanSeek) {
						MD5 md5 = new MD5CryptoServiceProvider();
						FileEtags[file] = System.BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", "").ToLower();
						this.Response.AddHeader("Etag", "\"" + FileEtags[file] + "\"");
						stream.Position = 0;
					}
					
					this.Response.ContentLength64 = stream.Length;
					
					Match ExtMatch = Regex.Match(file, "([^\\.]+)$");
					if (ExtMatch.Success) {
						switch(ExtMatch.Groups[1].Value) {
							case "html":
							case "xml":
							case "xsl":
							case "js":
							case "css":
								this.Response.ContentEncoding = Encoding.UTF8;
								break;
							case "gif":
							case "png":
							case "jpg":
							case "jpeg":
								this.Response.AppendHeader("Accept-Range", "bytes");
								break;
						}
						
						switch(ExtMatch.Groups[1].Value) {
							case "html":
								this.Response.ContentType = "text/html";
								break;
							case "xml":
								this.Response.ContentType = "text/xml";
								break;
							case "xsl":
								this.Response.ContentType = "text/xsl";
								break;
							case "js":
								this.Response.ContentType = "application/javascript";
								break;
							case "css":
								this.Response.ContentType = "text/css";
								break;
							case "gif":
								this.Response.ContentType = "image/gif";
								break;
							case "png":
								this.Response.ContentType = "image/png";
								break;
							case "jpg":
							case "jpeg":
								this.Response.ContentType = "image/jpeg";
								break;
						}
					}
					
					Byte[] data = new Byte[stream.Length];
					stream.Read(data, 0, (int)stream.Length);
					this.ResponseOutputStream.Write(data, 0, (int)stream.Length);
				} catch(Exception e) {
					this.Response.StatusCode = (int)HttpStatusCode.NotFound;
					logger.Error(e.Message);
				} finally {
					this.ResponseOutputStream.Close();
				}
			}
		}

		private bool Route(string file) {
			bool routed = false;

			switch(file) {
				case "/files/index.html":
					if (!this.User.IsLoggedIn) {
						this.Redirect("/login/");
						routed = true;
					}
					break;

				case "/files/logout.html":
					Cookie cookieUsername = new Cookie(User.COOKIE_USERNAME, "", "/");
					cookieUsername.Expired = true;
					cookieUsername.Expires = DateTime.Now.Subtract(new TimeSpan(1, 0, 0));
					Cookie cookiePassword = new Cookie(User.COOKIE_PASSWORD, "", "/");
					cookiePassword.Expired = true;
					cookiePassword.Expires = DateTime.Now.Subtract(new TimeSpan(1, 0, 0));
					this.Response.SetCookie(cookieUsername);
					this.Response.SetCookie(cookiePassword);
					this.Redirect("/");
					routed = true;
					break;
				
				case "/files/login.html":
					if (this.Request.HttpMethod == "POST") {
						using(HttpPostRequest.HttpPostRequest postRequest = new HttpPostRequest.HttpPostRequest(this.Request)) {
							if (postRequest.Parameters[User.COOKIE_USERNAME] != null && postRequest.Parameters[User.COOKIE_PASSWORD] != null) {
								User user = new User();
								if (user.RefreshByUsernamePassword(postRequest.Parameters[User.COOKIE_USERNAME], postRequest.Parameters[User.COOKIE_PASSWORD])) {
									this.Response.SetCookie(new Cookie(User.COOKIE_USERNAME, user.Username, "/"));
									this.Response.SetCookie(new Cookie(User.COOKIE_PASSWORD, user.Password, "/"));
									this.Redirect("/");
									routed = true;
								} else if (user.RefreshByEMailPassword(postRequest.Parameters[User.COOKIE_USERNAME], postRequest.Parameters[User.COOKIE_PASSWORD])) {
									this.Response.SetCookie(new Cookie(User.COOKIE_USERNAME, user.Username, "/"));
									this.Response.SetCookie(new Cookie(User.COOKIE_PASSWORD, user.Password, "/"));
									this.Redirect("/");
									routed = true;
								} else {
									if (postRequest.Parameters[User.COOKIE_USERNAME] == this.DefaultAdminUserName && postRequest.Parameters[User.COOKIE_PASSWORD] == this.DefaultAdminUserPassword) {
										user = new User(postRequest.Parameters[User.COOKIE_USERNAME], postRequest.Parameters[User.COOKIE_PASSWORD], UserAuthorization.Administrator, UserStatus.Active);
										this.Response.SetCookie(new Cookie(User.COOKIE_USERNAME, user.Username, "/"));
										this.Response.SetCookie(new Cookie(User.COOKIE_PASSWORD, user.Password, "/"));
										this.Redirect("/");
										routed = true;
									}
								}
							}
						}
					}
					break;
				
				case "/files/register.html":
					if (this.Request.HttpMethod == "POST") {
						using(HttpPostRequest.HttpPostRequest postRequest = new HttpPostRequest.HttpPostRequest(this.Request)) {
							if (postRequest.Parameters[User.COOKIE_USERNAME] != null && postRequest.Parameters[User.COOKIE_PASSWORD] != null && postRequest.Parameters["email_address"] != null) {
								if (!User.NameExists(postRequest.Parameters[User.COOKIE_USERNAME])) {
									if (!User.EMailExists(postRequest.Parameters["email_address"])) {
										User newUser = new User(postRequest.Parameters[User.COOKIE_USERNAME], postRequest.Parameters[User.COOKIE_PASSWORD], postRequest.Parameters["email_address"]);
										newUser.Add();
									}
								}
							}
						}
					}
					break;
			}

			return routed;
		}

		private void Redirect(string path) {
			try {
				if (this.Response != null) {
					this.Response.StatusCode = (int)HttpStatusCode.Redirect;
					this.Response.AddHeader("Location", path);

					this.ResponseOutputStream.Close();
				}
			} catch(Exception e) {
				logger.Trace(e.Message);
			}
		}

		public void OutputResult() {
			StringBuilder sb = new StringBuilder();
			this._doc.Save(new StringWriter(sb));
			
			try {
				byte[] buffer = Encoding.UTF8.GetBytes(sb.ToString());
				
				if (this.Response != null) {
					this.Response.ContentEncoding = Encoding.UTF8;
					this.Response.ContentType = "text/xml";
					this.Response.ContentLength64 = buffer.Length;
				}
				
				this.ResponseOutputStream.Write(buffer, 0, buffer.Length);
				this.ResponseOutputStream.Close();
			} catch(Exception e) {
				logger.Trace(e.Message);
			}
		}
	}
}

