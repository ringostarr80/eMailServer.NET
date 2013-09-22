using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using NLog;

namespace TcpRequestHandler {
	public class TcpRequestEventArgs : EventArgs {
		private IPEndPoint _remoteEndPoint = null;
		private IPEndPoint _localEndPoint = null;
		
		public IPEndPoint RemoteEndPoint { get { return this._remoteEndPoint; } }
		public IPEndPoint LocalEndPoint { get { return this._localEndPoint; } }
		
		public TcpRequestEventArgs(IPEndPoint remoteEndPoint, IPEndPoint localEndPoint) {
			this._remoteEndPoint = remoteEndPoint;
			this._localEndPoint = localEndPoint;
		}
	}
	
	public class TcpLineReceivedEventArgs : EventArgs {
		private string _line = String.Empty;
		private byte[] _rawBytes = new byte[] {};
		
		public string Line { get { return this._line; } }
		public byte[] RawBytes { get { return this._rawBytes; } }
		
		public TcpLineReceivedEventArgs(byte[] rawBytes) {
			this._rawBytes = rawBytes;
			this._line = Encoding.UTF8.GetString(rawBytes).Trim(new char[] {'\r', '\n'});
;		}
	}
	
	public delegate void TcpRequestEventHandler(object sender, TcpRequestEventArgs e);
	public delegate void TcpLineReceivedEventHandler(object sender, TcpLineReceivedEventArgs e);
	
	public class TcpRequestHandler : IRequestHandler {
		protected WaitHandle[] _waitHandles = new WaitHandle[] {
			new AutoResetEvent(false)
		};
		
		protected static Logger logger = LogManager.GetCurrentClassLogger();

		protected NetworkStream _stream = null;
		protected IPEndPoint _remoteEndPoint = null;
		protected IPEndPoint _localEndPoint = null;
		protected int _messageCounter = 0;
		protected bool _verbose = true;

		public bool Verbose { get { return this._verbose; } set { this._verbose = value; } }
		
		public event TcpRequestEventHandler Connected;
		public event TcpRequestEventHandler Disconnected;
		public event TcpLineReceivedEventHandler LineReceived;

		public TcpRequestHandler() {

		}

		public TcpRequestHandler(TcpClient client) {
			this._remoteEndPoint = (IPEndPoint)client.Client.RemoteEndPoint;
			this._localEndPoint = (IPEndPoint)client.Client.LocalEndPoint;

			this._stream = client.GetStream();
		}
		
		protected void OnTcpRequestConnected(TcpRequestEventArgs e) {
	        if (Connected != null) {
	            Connected(this, e);
	        }
        }
		
		protected void OnTcpRequestDisconnected(TcpRequestEventArgs e) {
	        if (Disconnected != null) {
	            Disconnected(this, e);
	        }
        }
		
		protected void OnTcpLineReceived(TcpLineReceivedEventArgs e) {
	        if (LineReceived != null) {
	            LineReceived(this, e);
	        }
        }

		protected void SendMessage(string message, string status) {
			byte[] msg = Encoding.UTF8.GetBytes(String.Format("{0} {1}\r\n", status, message));
			try {
				this._stream.Write(msg, 0, msg.Length);
				logger.Info(String.Format("[{0}:{1}] message sent: {2} {3}", this._remoteEndPoint.Address.ToString(), this._remoteEndPoint.Port, status, message));
			} catch(Exception) {
				logger.Error(String.Format("error writing message: {0} {1}", status, message));
			}
		}
		
		protected void SendMessage(string message, int status) {
			this.SendMessage(message, status.ToString());
		}
		
		public void Start() {
			this.OnTcpRequestConnected(new TcpRequestEventArgs(this._remoteEndPoint, this._localEndPoint));
			
			Byte[] bytes = new Byte[1024];
			int i = 0;
			bool lineSent = true;
			
			List<byte> listBytes = new List<byte>();
			try {
				while((i = this._stream.Read(bytes, 0, bytes.Length)) != 0) {
					for(int byteIndex = 0; byteIndex < i; byteIndex++) {
						if (bytes[byteIndex] != '\n') {
							listBytes.Add(bytes[byteIndex]);
							lineSent = false;
						} else {
							this.OnTcpLineReceived(new TcpLineReceivedEventArgs(listBytes.ToArray()));
							lineSent = true;
							listBytes = new List<byte>();
						}
					}
				}
				
				if (!lineSent) {
					this.OnTcpLineReceived(new TcpLineReceivedEventArgs(listBytes.ToArray()));
				}
			} catch(Exception) {
				logger.Error(String.Format("error reading data from stream."));
			}
		}
		
		public void Close() {
			((AutoResetEvent)this._waitHandles[0]).Set();
		}
		
		public void WaitForClosing() {
			WaitHandle.WaitAll(this._waitHandles);
		}

		public virtual void OutputResult() {
			
		}
	}
}

