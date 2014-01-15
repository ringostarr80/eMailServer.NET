using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace eMailServer {
	public class SmtpCommandReceivedEventArgs : EventArgs {
		private int _status = 0;
		private string _message = String.Empty;

		public int Status { get { return this._status; } }
		public string Message { get { return this._message; } }

		public SmtpCommandReceivedEventArgs(int status, string message) {
			this._status = status;
			this._message = message;
		}
	}

	public delegate void SmtpCommandReceivedEventHandler(object sender, SmtpCommandReceivedEventArgs e);

	public class SmtpRequestHandler : TcpRequestHandler.TcpRequestHandler {
		public event SmtpCommandReceivedEventHandler CommandReceived;

		public SmtpRequestHandler() : base() {
			
		}

		public SmtpRequestHandler(TcpClient client) : base(client) {
			this.Init();
		}

		public SmtpRequestHandler(TcpClient client, bool isServer) : base(client, isServer) {
			this.Init();
		}
		
		public SmtpRequestHandler(TcpClient client, int imapSslPort) : base(client, imapSslPort) {
			this.Init();
		}

		public SmtpRequestHandler(TcpClient client, int imapSslPort, bool isServer) : base(client, imapSslPort, isServer) {
			this.Init();
		}

		private void Init() {
			this.LineReceived += (object sender, TcpRequestHandler.TcpLineReceivedEventArgs e) => {
				Console.WriteLine("SmtpRequestHandler => line received: " + e.Line);
				Match statusMatch = Regex.Match(e.Line.Trim(), "^([0-9]+)\\s+(.+)", RegexOptions.Compiled);
				try {
					if (statusMatch.Success) {
						int status = Convert.ToInt32(statusMatch.Groups[1].Value);
						SmtpCommandReceivedEventArgs eventArgs;
						switch(status) {
							case 220: // Server bereit
								eventArgs = new SmtpCommandReceivedEventArgs(status, statusMatch.Groups[2].Value.Trim());
								break;

							default:
								eventArgs = new SmtpCommandReceivedEventArgs(status, statusMatch.Groups[2].Value.Trim());
								break;
						}
						this.OnSmtpCommandReceived(eventArgs);
					}
				} catch(Exception) {

				}
			};
		}

		protected void OnSmtpCommandReceived(SmtpCommandReceivedEventArgs e) {
			if (CommandReceived != null) {
				CommandReceived(this, e);
			}
		}

		public override void OutputResult() {
			this.SendMessage("closing channel", 221);
			base.OutputResult();
		}
	}
}

