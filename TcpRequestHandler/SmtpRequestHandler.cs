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
	public class SmtpRequestHandler : TcpRequestHandler.TcpRequestHandler {
		public SmtpRequestHandler() : base() {
			
		}

		public SmtpRequestHandler(TcpClient client) : base(client) {
			
		}
		
		public SmtpRequestHandler(TcpClient client, int imapSslPort) : base(client, imapSslPort) {
			
		}

		public override void OutputResult() {
			this.SendMessage("closing channel", 221);
			base.OutputResult();
		}
	}
}

