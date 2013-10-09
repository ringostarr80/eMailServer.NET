using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Mono.Security.Authenticode;
using Mono.Security.Protocol.Tls;
using NLog;

namespace TcpRequestHandler {
	public enum State {
		Default,
		AuthenticatePlain,
		AuthenticateCramMD5
	}

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
		private const byte InnerPadding = 0x36;
		private const byte OuterPadding = 0x5C;

		protected State _state = State.Default;
		private static string _serverCertificateFilename = String.Empty;
		private static string _serverKeyFilename = String.Empty;
		protected static X509Certificate _serverCertificate = null;
		protected int _sslPort = 993;
		protected WaitHandle[] _waitHandles = new WaitHandle[] {
			new AutoResetEvent(false)
		};
		protected static Logger logger = LogManager.GetCurrentClassLogger();
		protected NetworkStream _stream = null;
		protected SslServerStream _sslStream = null;
		protected bool _streamClosed = false;
		private Stream _currentUsedStream = null;
		protected IPEndPoint _remoteEndPoint = null;
		protected IPEndPoint _localEndPoint = null;
		protected int _messageCounter = 0;
		protected bool _verbose = true;
		protected string _currentCramMD5Challenge = String.Empty;
		public static string ServerCertificateFilename { get { return _serverCertificateFilename; } }
		public static string ServerKeyFilename { get { return _serverKeyFilename; } }

		public bool Verbose { get { return this._verbose; } set { this._verbose = value; } }
		public bool SslIsActive { get { return (this._sslStream != null) ? true : false; } }
		
		public event TcpRequestEventHandler Connected;
		public event TcpRequestEventHandler Disconnected;
		public event TcpLineReceivedEventHandler LineReceived;

		public TcpRequestHandler() {

		}
		
		public TcpRequestHandler(TcpClient client) {
			this.Init(client);
		}

		public TcpRequestHandler(TcpClient client, int sslPort) {
			this._sslPort = sslPort;
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
			this._remoteEndPoint = (IPEndPoint)client.Client.RemoteEndPoint;
			this._localEndPoint = (IPEndPoint)client.Client.LocalEndPoint;
			
			this._stream = client.GetStream();
			this._currentUsedStream = this._stream;
			
			if (this._localEndPoint.Port == this._sslPort) {
				this.StartTls();
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
				this._currentUsedStream.Write(msg, 0, msg.Length);
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

			TaskFactory factory = new TaskFactory();
			factory.StartNew(() => {
				Byte[] bytes = new Byte[1024];
				bool lineSent = true;
				int bytesRead = 0;

				List<byte> listBytes = new List<byte>();
				try {
					while((bytesRead = this._currentUsedStream.Read(bytes, 0, bytes.Length)) != 0) {
						for(int byteIndex = 0; byteIndex < bytesRead; byteIndex++) {
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
					if (!this._streamClosed) {
						logger.ErrorException(ex.Message, ex);
					}
				}
			}
			);
		}
		
		public void Close() {
			((AutoResetEvent)this._waitHandles[0]).Set();
		}
		
		public void WaitForClosing() {
			WaitHandle.WaitAll(this._waitHandles);
		}

		public virtual void OutputResult() {
			try {
				this._streamClosed = true;
				this._currentUsedStream.Close();
			} catch(Exception e) {
				logger.Trace(e.Message);
			} finally {
				this.OnTcpRequestDisconnected(new TcpRequestEventArgs(this._remoteEndPoint, this._localEndPoint));
				((AutoResetEvent)this._waitHandles[0]).Set();
			}
		}

		protected bool StartTls() {
			if (this.SslIsActive) {
				return true;
			}

			try {
				if (_serverCertificate == null) {
					Assembly assembly = Assembly.GetExecutingAssembly();
					Stream stream = assembly.GetManifestResourceStream("localhost.cer");
					byte[] certificateBytes = new byte[stream.Length];
					stream.Read(certificateBytes, 0, Convert.ToInt32(stream.Length));
					_serverCertificate = new X509Certificate(certificateBytes);
				}

				this._sslStream = new SslServerStream(this._stream, _serverCertificate, false, false);
				this._sslStream.PrivateKeyCertSelectionDelegate += new PrivateKeySelectionCallback((X509Certificate certificate, string targetHost) => {
					try {
						PrivateKey key;
						if (_serverKeyFilename == String.Empty) {
							Assembly assembly = Assembly.GetExecutingAssembly();
							Stream stream = assembly.GetManifestResourceStream("localhost.pvk");
							byte[] privateKeyBytes = new byte[stream.Length];
							stream.Read(privateKeyBytes, 0, Convert.ToInt32(stream.Length));
							key = new PrivateKey(privateKeyBytes, "");
							return key.RSA;
						} else {
							key = PrivateKey.CreateFromFile(_serverKeyFilename);
							return key.RSA;
						}
					} catch(Exception ex) {
						Console.WriteLine("Exception: " + ex.Message);
					}

					return null;
				}
				);
				this._currentUsedStream = this._sslStream;
				return true;
			} catch(Exception ex) {
				this._sslStream = null;
				logger.ErrorException(ex.Message, ex);
				return false;
			}
		}

		public static void SetServerCertificate(string filename) {
			if (filename != String.Empty) {
				if (File.Exists(filename)) {
					_serverCertificateFilename = filename;
					try {
						_serverCertificate = X509Certificate.CreateFromCertFile(_serverCertificateFilename);
					} catch(Exception ex) {
						logger.ErrorException(ex.Message, ex);
					}
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

		protected string CalculateOneTimeBase64Challenge(string hostname) {
			Process process = Process.GetCurrentProcess();
			TimeSpan unixTimestampSpan = (DateTime.Now - new DateTime(1970, 1, 1, 0, 0, 0, 0).ToLocalTime());
			
			string input = String.Format("<{0}.{1}@{2}>", process.Id, unixTimestampSpan.TotalSeconds, hostname);
			return Convert.ToBase64String(Encoding.UTF8.GetBytes(input));
		}

		protected List<string> GetWordsFromBase64EncodedLine(string line) {
			byte[] decodedBytes = Convert.FromBase64String(line);
			List<string> words = new List<string>();
			List<byte> byteWord = new List<byte>();
			foreach(byte currentByte in decodedBytes) {
				if (currentByte == 0) {
					if (byteWord.Count > 0) {
						words.Add(Encoding.UTF8.GetString(byteWord.ToArray()));
					}
								
					byteWord = new List<byte>();
					continue;
				}
							
				byteWord.Add(currentByte);
			}
			if (byteWord.Count > 0) {
				words.Add(Encoding.UTF8.GetString(byteWord.ToArray()));
			}
			
			return words;
		}

		/// <summary>
		/// Calculates the Cram MD5 digest.
		/// </summary>
		/// <returns>
		/// The cram MD5 digest.
		/// </returns>
		/// <param name='secret'>
		/// the secret password.
		/// </param>
		/// <param name='challenge'>
		/// the one-time challenge string.
		/// </param>
		/// <see cref="http://tools.ietf.org/html/rfc2104"/>
		protected string CalculateCramMD5Digest(string secret, string challenge) {
			//digest = MD5(('secret' XOR opad), MD5(('secret' XOR ipad), challenge))
			
			byte[] innerPadded = this.GetXorWithPad(Encoding.UTF8.GetBytes(secret), InnerPadding);
			byte[] outerPadded = this.GetXorWithPad(Encoding.UTF8.GetBytes(secret), OuterPadding);
			byte[] challengeBytes = Encoding.UTF8.GetBytes(challenge);
			byte[] innerPaddedAndChallenge = new byte[innerPadded.Length + challengeBytes.Length];
			for(int i = 0; i < innerPadded.Length; i++) {
				innerPaddedAndChallenge[i] = innerPadded[i];
			}
			for(int i = innerPadded.Length; i < innerPaddedAndChallenge.Length; i++) {
				innerPaddedAndChallenge[i] = challengeBytes[i - innerPadded.Length];
			}
			byte[] innerPaddedAndChallengeMD5 = this.CalculateMD5(innerPaddedAndChallenge);
			
			byte[] complete = new byte[outerPadded.Length + innerPaddedAndChallengeMD5.Length];
			for(int i = 0; i < outerPadded.Length; i++) {
				complete[i] = outerPadded[i];
			}
			for(int i = outerPadded.Length; i < complete.Length; i++) {
				complete[i] = innerPaddedAndChallengeMD5[i - outerPadded.Length];
			}
			
			byte[] completeMD5 = this.CalculateMD5(complete);
			return System.BitConverter.ToString(completeMD5).Replace("-", "").ToLower();
		}

		private byte[] GetXorWithPad(byte[] input, byte pad) {
			byte[] inputBytes = input;
			byte[] paddedInput = new byte[64];
			int maxLoopValue = (inputBytes.Length < paddedInput.Length) ? inputBytes.Length : paddedInput.Length;
			
			for(int i = 0; i < maxLoopValue; i++) {
				paddedInput[i] = (byte)(inputBytes[i] ^ pad);
			}
			if (maxLoopValue < paddedInput.Length) {
				for(int i = maxLoopValue; i < paddedInput.Length; i++) {
					paddedInput[i] = (byte)(0x00 ^ pad);
				}
			}
			
			return paddedInput;
		}

		protected byte[] CalculateMD5(byte[] input) {
			MD5 md5 = new MD5CryptoServiceProvider();
			return md5.ComputeHash(input);
		}
		
		protected byte[] CalculateMD5(string input) {
			return this.CalculateMD5(Encoding.Default.GetBytes(input));
		}
		
		protected string CalculateMD5String(string input) {
			return System.BitConverter.ToString(this.CalculateMD5(input)).Replace("-", "").ToLower();
		}
	}
}

