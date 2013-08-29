using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using NLog;

namespace eMailServer {
	public class SmtpRequestHandler {
		private static Logger logger = LogManager.GetCurrentClassLogger();

		private NetworkStream _stream = null;
		private IPEndPoint _remoteEndPoint = null;
		private IPEndPoint _localEndPoint = null;

		public SmtpRequestHandler() {

		}

		public SmtpRequestHandler(TcpClient client) {
			this._remoteEndPoint = (IPEndPoint)client.Client.RemoteEndPoint;
			this._localEndPoint = (IPEndPoint)client.Client.LocalEndPoint;
			if (eMailServer.Options.Verbose && this._remoteEndPoint != null && this._localEndPoint != null) {
				logger.Debug("connected from remote [{0}:{1}] to local [{2}:{3}]",
					this._remoteEndPoint.Address.ToString(),
				    this._remoteEndPoint.Port,
				    this._localEndPoint.Address.ToString(),
				    this._localEndPoint.Port
				);
			}

			this._stream = client.GetStream();

			this.SendMessage("service ready", 220);

			eMail mail = new eMail();

			Byte[] bytes = new Byte[1024];
			int i = 0;
			string incomingMessage = String.Empty;
			string mailMessage = String.Empty;
			bool dataStarted = false;
			bool dataFinished = false;
			while((i = this._stream.Read(bytes, 0, bytes.Length)) != 0) {
				string buffer = Encoding.UTF8.GetString(bytes, 0, i);
				if (eMailServer.Options.Verbose) {
					logger.Debug("Raw incoming string: " + buffer);
				}

				if (buffer.IndexOf("\n") != -1) {
					incomingMessage += buffer.Substring(0, buffer.IndexOf("\n") + 1);

					if (!dataStarted) {
						incomingMessage = incomingMessage.Trim();
						logger.Info(String.Format("[{0}:{1}] Received: \"{2}\"", this._remoteEndPoint.Address.ToString(), this._remoteEndPoint.Port, incomingMessage));

						if (incomingMessage.StartsWith("HELO ")) {
							mail.SetClientName(incomingMessage.Substring(5));
							this.SendMessage("OK", 250);
						} else if (incomingMessage.StartsWith("MAIL FROM:")) {
							mail.SetFrom(incomingMessage.Substring(10));
							this.SendMessage("OK", 250);
						} else if (incomingMessage.StartsWith("RCPT TO:")) {
							mail.SetRecipient(incomingMessage.Substring(8));
							this.SendMessage("OK", 250);
						} else if (incomingMessage == "DATA") {
							this.SendMessage("start mail input", 354);
							dataStarted = true;
							dataFinished = false;
						} else if (incomingMessage == "QUIT") {
							if (eMailServer.Options.Verbose) {
								logger.Debug("[{0}:{1}] quit connection", this._remoteEndPoint.Address.ToString(), this._remoteEndPoint.Port);
							}
							return;
						} else {
							this.SendMessage("Syntax error, command unrecognized", 500);
							if (eMailServer.Options.Verbose) {
								logger.Debug("[{0}:{1}] unknown command: {2}", this._remoteEndPoint.Address.ToString(), this._remoteEndPoint.Port, incomingMessage);
							}
						}
					} else {
						if (incomingMessage.Trim() == ".") {
							mailMessage = mailMessage.Trim();
							logger.Info("[{0}:{1}] eMail data received: {2}", this._remoteEndPoint.Address.ToString(), this._remoteEndPoint.Port, mailMessage);
							dataStarted = false;
							dataFinished = true;

							mail.ParseData(mailMessage);
							if (mail.IsValid) {
								mail.SaveToMongoDB();
							}

							this.SendMessage("OK", 250);
						} else {
							mailMessage += incomingMessage;
						}
					}

					if (!dataStarted || dataStarted && dataFinished) {
						incomingMessage = String.Empty;
					}
				} else {
					incomingMessage += buffer;
				}
			}
		}

		private void SendMessage(string message, int status) {
			byte[] msg = Encoding.UTF8.GetBytes(String.Format("{0} {1}\r\n", status, message));
			this._stream.Write(msg, 0, msg.Length);
			logger.Info(String.Format("[{0}:{1}] message sent: {2}", this._remoteEndPoint.Address.ToString(), this._remoteEndPoint.Port, message));
		}

		public void ProcessRequest() {

		}

		public void OutputResult() {
			try {
				this.SendMessage("closing channel", 221);
				this._stream.Close();
			} catch(Exception e) {
				logger.Trace(e.Message);
			}
		}
	}
}

