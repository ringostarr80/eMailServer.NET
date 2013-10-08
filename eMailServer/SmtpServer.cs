using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using NLog;
using TcpRequestHandler;

namespace eMailServer {
	public class SmtpServer : SmtpRequestHandler {
		protected new static Logger logger = LogManager.GetCurrentClassLogger();
		//private User _user = new User();
		//private string _currentCramMD5Challenge = String.Empty;

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

				if (!dataStarted) {
					if (e.Line.StartsWith("HELO ")) {
						mail.SetClientName(e.Line.Substring(5));
						this.SendMessage("OK", 250);
					} else if (e.Line.StartsWith("EHLO ")) {
						mail.SetClientName(e.Line.Substring(5));
						this.SendMessage("Hello " + mail.ClientName, 250);
						string capabilities = "AUTH LOGIN";
						if (!this.SslIsActive) {
							capabilities += " STARTTLS";
						}
						capabilities += " PLAIN CRAM-MD5";
						this.SendMessage(capabilities, 250);
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
			};
		}
	}
}

