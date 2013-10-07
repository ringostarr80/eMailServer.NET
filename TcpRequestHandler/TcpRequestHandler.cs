using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
//using System.Security;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Mono.Security.Authenticode;
using Mono.Security.Protocol.Tls;
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
		}
	}
	
	public delegate void TcpRequestEventHandler(object sender, TcpRequestEventArgs e);

	public delegate void TcpLineReceivedEventHandler(object sender, TcpLineReceivedEventArgs e);
	
	public class TcpRequestHandler : IRequestHandler {
		private static string _serverCertificateFilename = String.Empty;
		private static string _serverKeyFilename = String.Empty;
		protected static X509Certificate _serverCertificate = null;
		protected int _imapSslPort = 993;
		protected WaitHandle[] _waitHandles = new WaitHandle[] {
			new AutoResetEvent(false)
		};
		protected static Logger logger = LogManager.GetCurrentClassLogger();
		protected NetworkStream _stream = null;
		protected IPEndPoint _remoteEndPoint = null;
		protected IPEndPoint _localEndPoint = null;
		protected int _messageCounter = 0;
		protected bool _verbose = true;
		protected Mono.Security.Protocol.Tls.SslServerStream _sslStream = null;
		public static string ServerCertificateFilename { get { return _serverCertificateFilename; } }
		public static string ServerKeyFilename { get { return _serverKeyFilename; } }

		public bool Verbose { get { return this._verbose; } set { this._verbose = value; } }
		
		public event TcpRequestEventHandler Connected;
		public event TcpRequestEventHandler Disconnected;
		public event TcpLineReceivedEventHandler LineReceived;

		public TcpRequestHandler() {

		}
		
		public TcpRequestHandler(TcpClient client) {
			this.Init(client);
		}

		public TcpRequestHandler(TcpClient client, int imapSslPort) {
			this._imapSslPort = imapSslPort;
			this.Init(client);
		}

		/// <summary>
		/// Inititalizing the Request.
		/// </summary>
		/// <param name='client'>
		/// the TcpClient.
		/// </param>
		/// <see cref="http://docs.go-mono.com/?link=T%3aMono.Security.Protocol.Tls.SslServerStream"/>
		private void Init(TcpClient client) {
			//bool leaveInnerStreamOpen = false;
			
			this._remoteEndPoint = (IPEndPoint)client.Client.RemoteEndPoint;
			this._localEndPoint = (IPEndPoint)client.Client.LocalEndPoint;
			
			this._stream = client.GetStream();
			
			if (this._localEndPoint.Port == this._imapSslPort) {
				try {
					//RemoteCertificateValidationCallback validationCallback = new RemoteCertificateValidationCallback(this.ClientValidationCallback);
					//LocalCertificateSelectionCallback selectionCallback = new LocalCertificateSelectionCallback(ServerCertificateSelectionCallback);
				
					//this._sslStream = SslStream(this._stream, leaveInnerStreamOpen, validationCallback, selectionCallback);
					this._sslStream = new SslServerStream(this._stream, _serverCertificate, false, false);
					this._sslStream.PrivateKeyCertSelectionDelegate += new PrivateKeySelectionCallback((X509Certificate certificate, string targetHost) => {
						try {
							PrivateKey key = PrivateKey.CreateFromFile(_serverKeyFilename);
							return key.RSA;
						} catch(Exception ex) {
							Console.WriteLine("Exception: " + ex.Message);
						}

						return null;
					}
					);
				} catch(Exception ex) {
					logger.ErrorException(ex.Message, ex);
				}
			}
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
				if (this._localEndPoint.Port == this._imapSslPort) {
					this._sslStream.Write(msg, 0, msg.Length);
				} else {
					this._stream.Write(msg, 0, msg.Length);
				}
				logger.Info(String.Format("[{0}:{1}] to [{2}:{3}] message sent: {4} {5}", this._localEndPoint.Address.ToString(), this._localEndPoint.Port, this._remoteEndPoint.Address.ToString(), this._remoteEndPoint.Port, status, message));
			} catch(Exception) {
				logger.Error(String.Format("writing message: {0} {1}", status, message));
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
				Stream currentStream = (this._localEndPoint.Port == this._imapSslPort) ? (Stream)this._sslStream : (Stream)this._stream;
				while((i = currentStream.Read(bytes, 0, bytes.Length)) != 0) {
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
			} catch(Exception ex) {
				logger.ErrorException(ex.Message, ex);
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

		public static void SetServerCertificate(string filename) {
			if (filename != String.Empty) {
				if (File.Exists(filename)) {
					_serverCertificateFilename = filename;
					_serverCertificate = X509Certificate.CreateFromCertFile(_serverCertificateFilename);
				}
			}
		}

		public static void SetServerKeyFile(string filename) {
			if (filename != String.Empty) {
				if (File.Exists(filename)) {
					_serverKeyFilename = filename;
				}
			}
		}
	}
}

