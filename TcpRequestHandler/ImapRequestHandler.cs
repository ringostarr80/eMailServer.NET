using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;

namespace TcpRequestHandler {
	public enum ImapState {
		Default,
		AuthenticatePlain,
		AuthenticateCramMD5
	}
	
	public class ImapRequestHandler : TcpRequestHandler {
		private const byte InnerPadding = 0x36;
		private const byte OuterPadding = 0x5C;
		
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
			try {
				this.OnTcpRequestDisconnected(new TcpRequestEventArgs(this._remoteEndPoint, this._localEndPoint));
				if (this._localEndPoint.Port == this._imapSslPort) {
					this._sslStream.Close();
				} else {
					this._stream.Close();
				}
			} catch(Exception e) {
				logger.Trace(e.Message);
			} finally {
				((AutoResetEvent)this._waitHandles[0]).Set();
			}
		}
		
		protected string CalculateOneTimeBase64Challenge(string hostname) {
			Process process = Process.GetCurrentProcess();
			TimeSpan unixTimestampSpan = (DateTime.Now - new DateTime(1970, 1, 1, 0, 0, 0, 0).ToLocalTime());
			
			string input = String.Format("<{0}.{1}@{2}>", process.Id, unixTimestampSpan.TotalSeconds, hostname);
			return Convert.ToBase64String(Encoding.UTF8.GetBytes(input));
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
	}
}
