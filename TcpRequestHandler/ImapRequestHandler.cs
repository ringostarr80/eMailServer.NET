using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;

namespace TcpRequestHandler {
	public class ImapRequestHandler : TcpRequestHandler {
		public ImapRequestHandler() : base() {
			
		}

		public ImapRequestHandler(TcpClient client) : base(client) {
			
		}
		
		public ImapRequestHandler(TcpClient client, int imapSslPort) : base(client, imapSslPort) {
			
		}
		
		protected FetchFields ParseFetchFields(string fetch) {
			return new FetchFields(fetch); 
		}

		public override void OutputResult() {
			base.OutputResult();
		}
	}
}
