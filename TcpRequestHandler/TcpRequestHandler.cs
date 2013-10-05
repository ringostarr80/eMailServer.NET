using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
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
		}
	}
	
	public delegate void TcpRequestEventHandler(object sender, TcpRequestEventArgs e);

	public delegate void TcpLineReceivedEventHandler(object sender, TcpLineReceivedEventArgs e);
	
	public class TcpRequestHandler : IRequestHandler {
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
		protected SslStream _sslStream = null;
		public static string ServerCertificateFilename = String.Empty;

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
		
		private void Init(TcpClient client) {
			bool leaveInnerStreamOpen = false;
			
			this._remoteEndPoint = (IPEndPoint)client.Client.RemoteEndPoint;
			this._localEndPoint = (IPEndPoint)client.Client.LocalEndPoint;
			
			this._stream = client.GetStream();
			
			if (this._localEndPoint.Port == this._imapSslPort) {
				try {
					Console.WriteLine("remote-port: " + this._remoteEndPoint.Port + "; local-port: " + this._localEndPoint.Port);
					RemoteCertificateValidationCallback validationCallback = new RemoteCertificateValidationCallback(this.ClientValidationCallback);
					LocalCertificateSelectionCallback selectionCallback = new LocalCertificateSelectionCallback(ServerCertificateSelectionCallback);
				
					this._sslStream = new SslStream(this._stream, leaveInnerStreamOpen, validationCallback, selectionCallback);
					Console.WriteLine("before ServerSideHandshake");
					this.ServerSideHandshake();
					Console.WriteLine("after ServerSideHandshake");
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
				logger.Info(String.Format("[{0}:{1}] message sent: {2} {3}", this._remoteEndPoint.Address.ToString(), this._remoteEndPoint.Port, status, message));
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
		
		protected bool ClientValidationCallback(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors) {
			Console.WriteLine("ClientValidationCallback(...)");
			return true;
		}
		
		protected X509Certificate ServerCertificateSelectionCallback(object sender, string targetHost, X509CertificateCollection localCertificates, X509Certificate remoteCertificate, string[] acceptableIssuers) {
			Console.WriteLine("ServerCertificateSelectionCallback(...)");
			return new X509Certificate();
		}
		
		/// <summary>
		/// Perform the server handshake
		/// </summary>
		private void ServerSideHandshake() {
			SecureString secureString = new SecureString();
			secureString.AppendChar('l');
			secureString.AppendChar('x');
			secureString.AppendChar('5');
			secureString.AppendChar('-');
			secureString.AppendChar('I');
			secureString.AppendChar('h');
			secureString.AppendChar('T');
			secureString.AppendChar('F');
			secureString.AppendChar('a');
			secureString.AppendChar('1');
			secureString.AppendChar('0');
			secureString.AppendChar('0');
			secureString.AppendChar('5');
			secureString.AppendChar('2');
			secureString.AppendChar('0');
			secureString.AppendChar('1');
			secureString.AppendChar('3');
			secureString.AppendChar('.');
			if (_serverCertificate == null && ServerCertificateFilename != String.Empty) {
				//_serverCertificate = X509Certificate2.CreateFromCertFile(ServerCertificateFilename);
				//X509Certificate2 certificate = new X509Certificate2(ServerCertificateFilename, secureString, X509KeyStorageFlags.UserProtected);
				//_serverCertificate = X509Certificate2.CreateFromCertFile(ServerCertificateFilename);
				_serverCertificate = new X509Certificate2(ServerCertificateFilename, secureString);
			}
 
			bool requireClientCertificate = false;
			SslProtocols enabledSslProtocols = SslProtocols.Ssl2 | SslProtocols.Ssl3 | SslProtocols.Tls;
			bool checkCertificateRevocation = true;
			
			try {
				Console.WriteLine("before SslStream.AuthenticateAsServer");
				this._sslStream.AuthenticateAsServer(_serverCertificate, requireClientCertificate, enabledSslProtocols, checkCertificateRevocation);
				Console.WriteLine("after SslStream.AuthenticateAsServer");
				this.DisplaySecurityLevel(this._sslStream);
			} catch(AuthenticationException ex) {
				logger.ErrorException(ex.Message, ex);
			} catch(Exception ex) {
				logger.ErrorException(ex.Message, ex);
			}
		}
		
		private void DisplaySecurityLevel(SslStream stream) {
			Console.WriteLine("Cipher: {0} strength {1}", stream.CipherAlgorithm, stream.CipherStrength);
			Console.WriteLine("Hash: {0} strength {1}", stream.HashAlgorithm, stream.HashStrength);
			Console.WriteLine("Key exchange: {0} strength {1}", stream.KeyExchangeAlgorithm, stream.KeyExchangeStrength);
			Console.WriteLine("Protocol: {0}", stream.SslProtocol);
		}
	}
}

