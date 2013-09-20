using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using NLog;

namespace TcpRequestHandler {
	public class TcpRequestHandler : IRequestHandler {
		protected static Logger logger = LogManager.GetCurrentClassLogger();

		protected NetworkStream _stream = null;
		protected IPEndPoint _remoteEndPoint = null;
		protected IPEndPoint _localEndPoint = null;
		protected int _messageCounter = 0;
		protected bool _verbose = true;

		public bool Verbose { get { return this._verbose; } set { this._verbose = value; } }

		public TcpRequestHandler() {

		}

		public TcpRequestHandler(TcpClient client) {
			this._remoteEndPoint = (IPEndPoint)client.Client.RemoteEndPoint;
			this._localEndPoint = (IPEndPoint)client.Client.LocalEndPoint;
			if (this.Verbose && this._remoteEndPoint != null && this._localEndPoint != null) {
				logger.Debug("connected from remote [{0}:{1}] to local [{2}:{3}]",
					this._remoteEndPoint.Address.ToString(),
				    this._remoteEndPoint.Port,
				    this._localEndPoint.Address.ToString(),
				    this._localEndPoint.Port
				);
			}

			this._stream = client.GetStream();
		}

		protected void SendMessage(string message, int status) {
			byte[] msg = Encoding.UTF8.GetBytes(String.Format("{0} {1}\r\n", status, message));
			try {
				this._stream.Write(msg, 0, msg.Length);
			} catch(Exception e) {
				Console.WriteLine("Exception: " + e.Message);
			}
			logger.Info(String.Format("[{0}:{1}] message sent: {2}", this._remoteEndPoint.Address.ToString(), this._remoteEndPoint.Port, message));
		}

		protected void SendMessage(string message, string status) {
			byte[] msg = Encoding.UTF8.GetBytes(String.Format("{0} {1}\r\n", status, message));
			this._stream.Write(msg, 0, msg.Length);
			logger.Info(String.Format("[{0}:{1}] message sent: {2} {3}", this._remoteEndPoint.Address.ToString(), this._remoteEndPoint.Port, status, message));
		}

		public virtual void ProcessRequest() {

		}

		public virtual void OutputResult() {

		}
	}
}

