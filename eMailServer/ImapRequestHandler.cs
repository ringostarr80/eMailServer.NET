using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using NLog;

namespace eMailServer {
	public class ImapRequestHandler : IRequestHandler {
		private static Logger logger = LogManager.GetCurrentClassLogger();

		private NetworkStream _stream = null;
		private IPEndPoint _remoteEndPoint = null;
		private IPEndPoint _localEndPoint = null;
		private int _messageCounter = 0;

		public ImapRequestHandler() {

		}

		public ImapRequestHandler(TcpClient client) {
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

			this.SendMessage("OK IMAP4rev1 Service Ready", "*");

			Byte[] bytes = new Byte[1024];
			char[] trimChars = new char[] {'\r', '\n'};
			int i = 0;
			string incomingMessage = String.Empty;
			string lastLine = String.Empty;
			string lastClientId = String.Empty;

			while((i = this._stream.Read(bytes, 0, bytes.Length)) != 0) {
				bool breakWhileLoop = false;
				bool lastLineHasLineEnding = false;
				List<string> lines = new List<string>();
				int byteStartIndex = 0;
				for(int byteIndex = 0; byteIndex < i; byteIndex++) {
					if (bytes[byteIndex] == '\n') {
						string currentline = Encoding.UTF8.GetString(bytes, byteStartIndex, byteIndex + 1 - byteStartIndex).Trim(trimChars);
						if (lines.Count == 0 && lastLine != String.Empty) {
							currentline = lastLine + currentline;
						}
						if (eMailServer.Options.Verbose) {
							logger.Debug("Raw incoming line: " + currentline);
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
							if (eMailServer.Options.Verbose) {
								logger.Debug("Raw incoming line: " + currentline);
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
					bool breakLoop = false;

					incomingMessage += lines[lineIndex];

					if (lineIndex == lines.Count - 1 && !lastLineHasLineEnding) {
						lastLine = lines[lineIndex];
						break;
					}

					logger.Info(String.Format("[{0}:{1}] Received: \"{2}\"", this._remoteEndPoint.Address.ToString(), this._remoteEndPoint.Port, lines[lineIndex]));

					Match clientCommandMatch = Regex.Match(lines[lineIndex], @"^([^\s]+)\s+(\w+)(\s.+)?", RegexOptions.IgnoreCase);
					if (clientCommandMatch.Success) {
						lastClientId = clientCommandMatch.Groups[1].Value;

						switch(clientCommandMatch.Groups[2].Value.ToUpper()) {
							case "AUTHENTICATE":
								string trimmedRest = clientCommandMatch.Groups[3].Value.Trim();
								switch(trimmedRest.ToUpper()) {
									case "PLAIN":
										this.SendMessage("", "+");
										break;
									
									default:
										this.SendMessage("BAD " + clientCommandMatch.Groups[2].Value + " invalid authenticate method", lastClientId);
										break;
								}
								break;

							case "CAPABILITY":
								this.SendMessage("CAPABILITY IMAP4rev1 LOGINDISABLED AUTH=PLAIN", "*");
								this.SendMessage("OK CAPABILITY completed", lastClientId);
								break;
							
							case "LOGOUT":
								this.SendMessage("BYE IMAP4rev1 server terminating connection", "*");
								this.SendMessage("OK LOGOUT completed", String.Format("m{0:000}", ++this._messageCounter));
								breakLoop = true;
								breakWhileLoop = true;
								break;
							
							case "STARTTLS":
								this.SendMessage("OK STARTTLS completed", lastClientId);
								break;
							
							default:
								this.SendMessage("BAD " + clientCommandMatch.Groups[2].Value + " command not found", lastClientId);
								break;
						}
					} else {
						/*
						byte[] decodedBytes = Convert.FromBase64String(lines[lineIndex]);
						Console.WriteLine("Base64 Decoded Bytes:");
						foreach(byte decodedByte in decodedBytes) {
							Console.Write(" " + decodedByte);
						}
						Console.WriteLine();
						*/
						this.SendMessage("BAD invalid command line: " + lines[lineIndex], lastClientId);
					}

					if (breakLoop) {
						break;
					}
				}

				if (breakWhileLoop) {
					break;
				}
			}
		}

		private void SendMessage(string message, int status) {
			byte[] msg = Encoding.UTF8.GetBytes(String.Format("{0} {1}\r\n", status, message));
			this._stream.Write(msg, 0, msg.Length);
			logger.Info(String.Format("[{0}:{1}] message sent: {2}", this._remoteEndPoint.Address.ToString(), this._remoteEndPoint.Port, message));
		}

		private void SendMessage(string message, string status) {
			byte[] msg = Encoding.UTF8.GetBytes(String.Format("{0} {1}\r\n", status, message));
			this._stream.Write(msg, 0, msg.Length);
			logger.Info(String.Format("[{0}:{1}] message sent: {2} {3}", this._remoteEndPoint.Address.ToString(), this._remoteEndPoint.Port, status, message));
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
