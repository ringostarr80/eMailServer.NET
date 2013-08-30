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

			Byte[] bytes = new Byte[10];
			char[] trimChars = new char[] {'\r', '\n'};
			int i = 0;
			string incomingMessage = String.Empty;
			string mailMessage = String.Empty;
			string lastLine = String.Empty;
			bool dataStarted = false;
			bool dataFinished = false;

			while((i = this._stream.Read(bytes, 0, bytes.Length)) != 0) {
				bool lastLineHasLineEnding = false;
				List<string> lines = new List<string>();
				int byteStartIndex = 0;
				for(int byteIndex = 0; byteIndex < i; byteIndex++) {
					if (bytes[byteIndex] == '\n') {
						string currentline = Encoding.UTF8.GetString(bytes, byteStartIndex, byteIndex + 1 - byteStartIndex).Trim(trimChars);
						if (lines.Count == 0 && lastLine != String.Empty) {
							currentline = lastLine + currentline;
						}
						lines.Add(currentline);
						byteStartIndex = byteIndex;
						if (byteIndex == i - 1) {
							lastLineHasLineEnding = true;
						}
					} else if (byteIndex == i - 1) {
						string currentline = Encoding.UTF8.GetString(bytes, byteStartIndex, byteIndex + 1 - byteStartIndex).Trim(trimChars);
						if (lines.Count == 0 && lastLine != String.Empty) {
							currentline = lastLine + currentline;
						}
						lines.Add(currentline);
					}
				}

				lastLine = String.Empty;
				string buffer = Encoding.UTF8.GetString(bytes, 0, i);
				if (eMailServer.Options.Verbose) {
					logger.Debug("Raw incoming string: " + buffer);
				}

				for(int lineIndex = 0; lineIndex < lines.Count; lineIndex++) {
					incomingMessage += lines[lineIndex];

					if (lineIndex == lines.Count - 1 && !lastLineHasLineEnding) {
						lastLine = lines[lineIndex];
						break;
					}

					if (!dataStarted) {
						logger.Info(String.Format("[{0}:{1}] Received: \"{2}\"", this._remoteEndPoint.Address.ToString(), this._remoteEndPoint.Port, lines[lineIndex]));

						if (lines[lineIndex].StartsWith("HELO ")) {
							mail.SetClientName(lines[lineIndex].Substring(5));
							this.SendMessage("OK", 250);
						} else if (lines[lineIndex].StartsWith("MAIL FROM:")) {
							mail.SetFrom(lines[lineIndex].Substring(10));
							this.SendMessage("OK", 250);
						} else if (lines[lineIndex].StartsWith("RCPT TO:")) {
							mail.SetRecipient(lines[lineIndex].Substring(8));
							this.SendMessage("OK", 250);
						} else if (lines[lineIndex] == "DATA") {
							this.SendMessage("start mail input", 354);
							dataStarted = true;
							dataFinished = false;
						} else if (lines[lineIndex] == "QUIT") {
							if (eMailServer.Options.Verbose) {
								logger.Debug("[{0}:{1}] quit connection", this._remoteEndPoint.Address.ToString(), this._remoteEndPoint.Port);
							}
							return;
						} else {
							this.SendMessage("Syntax error, command unrecognized", 500);
							if (eMailServer.Options.Verbose) {
								logger.Debug("[{0}:{1}] unknown command: {2}", this._remoteEndPoint.Address.ToString(), this._remoteEndPoint.Port, lines[lineIndex]);
							}
						}
					} else {
						if (lines[lineIndex] == ".") {
							mailMessage = mailMessage.Trim();
							logger.Info("[{0}:{1}] eMail data received: {2}", this._remoteEndPoint.Address.ToString(), this._remoteEndPoint.Port, mailMessage);
							dataStarted = false;
							dataFinished = true;

							mail.ParseData(mailMessage);
							if (mail.IsValid) {
								mail.SaveToMongoDB();
							} else {
								logger.Error("received message is invalid for saving to database.");
							}

							this.SendMessage("OK", 250);
						} else {
							mailMessage += lines[lineIndex] + "\r\n";
						}
					}

					if (!dataStarted || dataFinished) {
						incomingMessage = String.Empty;
					}
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

