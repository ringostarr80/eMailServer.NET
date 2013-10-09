using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using NLog;
using TcpRequestHandler;

namespace eMailServer {
	public class SmtpServer : SmtpRequestHandler {
		protected new static Logger logger = LogManager.GetCurrentClassLogger();
		private User _user = new User();

		public SmtpServer() : base() {

		}

		public SmtpServer(TcpClient client, int sslPort) : base(client, sslPort) {
			bool dataStarted = false;
			string mailMessage = String.Empty;

			eMail mail = new eMail();

			this.Connected += (object sender, TcpRequestEventArgs e) => {
				if (this.Verbose && e.RemoteEndPoint != null && e.LocalEndPoint != null) {
					logger.Debug("connected from remote [{0}:{1}] to local [{2}:{3}]",
						e.RemoteEndPoint.Address.ToString(),
					    e.RemoteEndPoint.Port,
					    e.LocalEndPoint.Address.ToString(),
					    e.LocalEndPoint.Port
					);
				}

				this.SendMessage("service ready", 220);
			};
			
			this.Disconnected += (object sender, TcpRequestEventArgs e) => {
				if (this.Verbose && e.RemoteEndPoint != null && e.LocalEndPoint != null) {
					logger.Debug("disconnected from remote [{0}:{1}] to local [{2}:{3}]",
						e.RemoteEndPoint.Address.ToString(),
					    e.RemoteEndPoint.Port,
					    e.LocalEndPoint.Address.ToString(),
					    e.LocalEndPoint.Port
					);
				}
			};

			this.LineReceived += (object sender, TcpLineReceivedEventArgs e) => {
				logger.Info(String.Format("[{0}:{1}] to [{2}:{3}] Received Line: \"{4}\"", this._remoteEndPoint.Address.ToString(), this._remoteEndPoint.Port, this._localEndPoint.Address.ToString(), this._localEndPoint.Port, e.Line));

				switch(this._state) {
					case State.AuthenticateCramMD5:
						List<string> wordsCramMD5 = this.GetWordsFromBase64EncodedLine(e.Line);
						
						this._state = State.Default;
						if (wordsCramMD5.Count == 1) {
							string[] splittedWords = wordsCramMD5[0].Split(new char[] {' '});
							if (splittedWords.Length == 2) {
								bool nameExists = User.NameExists(splittedWords[0]);
								bool eMailExists = User.EMailExists(splittedWords[0]);
								if (nameExists || eMailExists) {
									string userId = (nameExists) ? User.GetIdByName(splittedWords[0]) : User.GetIdByEMail(splittedWords[0]);
									User tmpUser = new User();
									if (tmpUser.RefreshById(userId)) {
										string calculatedDigest = this.CalculateCramMD5Digest(tmpUser.Password, this._currentCramMD5Challenge);
										if (calculatedDigest == splittedWords[1]) {
											this._user = tmpUser;
											this.SendMessage("2.7.0 Authentication Succeeded", 235);
										} else {
											this.SendMessage("5.7.8 Authentication credentials invalid", 535);
										}
									} else {
										this.SendMessage("4.7.0 Temporary authentication failure", 454);
									}
								} else {
									this.SendMessage("5.7.8 Authentication credentials invalid", 535);
								}
							} else {
								this.SendMessage("5.7.8 Authentication credentials invalid", 535);
							}
						} else {
							this.SendMessage("5.7.8 Authentication credentials invalid", 535);
						}
						
						this._currentCramMD5Challenge = String.Empty;
						break;

					case State.Default:
					default:
						if (!dataStarted) {
							if (e.Line.StartsWith("HELO ")) {
								mail.SetClientName(e.Line.Substring(5));
								this.SendMessage("OK", 250);
							} else if (e.Line.StartsWith("EHLO ")) {
								mail.SetClientName(e.Line.Substring(5));
								this.SendMessage("Hello " + mail.ClientName + " [" + this._remoteEndPoint.Address.ToString() + "]", "250-localhost");
								if (!this.SslIsActive) {
									string capabilities = "LOGIN";
									capabilities += " PLAIN CRAM-MD5";
									this.SendMessage(capabilities, "250-AUTH");
									this.SendMessage("STARTTLS", 250);
								} else {
									string capabilities = "AUTH LOGIN";
									capabilities += " PLAIN CRAM-MD5";
									this.SendMessage(capabilities, 250);
								}
							} else if (e.Line.StartsWith("AUTH ")) {
								Match authMatch = Regex.Match(e.Line, @"^AUTH\s+(PLAIN|CRAM-MD5)(.*)?", RegexOptions.IgnoreCase);
								if (authMatch.Success) {
									switch(authMatch.Groups[1].Value.ToUpper()) {
										case "PLAIN":
											List<string> words = new List<string>();
											try {
												words = this.GetWordsFromBase64EncodedLine(authMatch.Groups[2].Value);
											} catch(Exception) {

											}
						
											this._state = State.Default;
											if (words.Count == 2) {
												if (words[0] != String.Empty && words[1] != String.Empty) {
													if (this._user.RefreshByUsernamePassword(words[0], words[1]) || this._user.RefreshByEMailPassword(words[0], words[1])) {
														this.SendMessage("2.7.0 Authentication Succeeded", 235);
													} else {
														this.SendMessage("5.7.8 Authentication credentials invalid", 535);
													}
												} else {
													this.SendMessage("5.7.8 Authentication credentials invalid", 535);
												}
											} else {
												this.SendMessage("5.7.8 Authentication credentials invalid", 535);
											}
											break;

										case "CRAM-MD5":
											this._state = State.AuthenticateCramMD5;
											string base64EncodedCramMD5Challenge = this.CalculateOneTimeBase64Challenge("localhost.de");
											this._currentCramMD5Challenge = Encoding.UTF8.GetString(Convert.FromBase64String(base64EncodedCramMD5Challenge));
											this.SendMessage(base64EncodedCramMD5Challenge, 334);
											break;

										default:
											this.SendMessage("Unrecognized authentication type", 504);
											break;
									}
								}
							} else if (e.Line.StartsWith("MAIL FROM:")) {
								mail.SetFrom(e.Line.Substring(10));
								this.SendMessage("OK", 250);
							} else if (e.Line.StartsWith("RCPT TO:")) {
								mail.SetRecipient(e.Line.Substring(8));
								this.SendMessage("OK", 250);
							} else if (e.Line.StartsWith("STARTTLS")) {
								if (e.Line.Trim() == "STARTTLS") {
									this.SendMessage("Ready to start TLS", 220);
									if (!this.StartTls()) {
										this.SendMessage("TLS not available due to temporary reason", 454);
									}
								} else {
									this.SendMessage("Syntax error (no parameters allowed)", 501);
								}
							} else if (e.Line == "DATA") {
								this.SendMessage("start mail input", 354);
								dataStarted = true;
							} else if (e.Line == "QUIT") {
								if (eMailServer.Options.Verbose) {
									logger.Debug("[{0}:{1}] to [{2}:{3}] quit connection", this._localEndPoint.Address.ToString(), this._localEndPoint.Port, this._remoteEndPoint.Address.ToString(), this._remoteEndPoint.Port);
								}
								this.Close();
								return;
							} else {
								this.SendMessage("Syntax error, command unrecognized", 500);
								if (eMailServer.Options.Verbose) {
									logger.Debug("[{0}:{1}] to [{2}:{3}] unknown command: {2}", this._remoteEndPoint.Address.ToString(), this._remoteEndPoint.Port, this._localEndPoint.Address.ToString(), this._localEndPoint.Port, e.Line);
								}
							}
						} else {
							if (e.Line == ".") {
								mailMessage = mailMessage.Trim();
								logger.Info("[{0}:{1}] to [{2}:{3}] eMail data received: {2}", this._remoteEndPoint.Address.ToString(), this._remoteEndPoint.Port, mailMessage, this._localEndPoint.Address.ToString(), this._localEndPoint.Port);
								dataStarted = false;

								mail.ParseData(mailMessage);
								if (mail.IsValid) {
									mail.SaveToMongoDB();
								} else {
									logger.Error("received message is invalid for saving to database.");
								}

								this.SendMessage("OK", 250);
							} else {
								mailMessage += e.Line + "\r\n";
							}
						}
						break;
				}
			};
		}
	}
}

