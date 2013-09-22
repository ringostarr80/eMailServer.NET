using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace TcpRequestHandler {
	public enum ImapState {
		Default,
		AuthenticatePlain
	}
	
	public class ImapRequestHandler : TcpRequestHandler {
		public ImapRequestHandler() : base() {

		}

		public ImapRequestHandler(TcpClient client) : base(client) {
			
		}
		
		protected FetchFields ParseFetchFields(string fetch) {
			return new FetchFields(fetch); 
		}

		public override void OutputResult() {
			try {
				this.OnTcpRequestDisconnected(new TcpRequestEventArgs(this._remoteEndPoint, this._localEndPoint));
				this._stream.Close();
			} catch(Exception e) {
				logger.Trace(e.Message);
			} finally {
				((AutoResetEvent)this._waitHandles[0]).Set();
			}
		}
	}
}

