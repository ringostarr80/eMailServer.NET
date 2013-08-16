using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Security.Cryptography;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace HttpPostRequest {
	public class HttpPostRequest : IDisposable {
		private string _encType = String.Empty;
		private string _boundary = String.Empty;
		private NameValueCollection _parameters = new NameValueCollection();
		private Dictionary<string, HttpPostFile> _files = new Dictionary<string, HttpPostFile>();

		public string EncodingType { get { return this._encType; } }
		public string Boundary { get { return this._boundary; } }
		public NameValueCollection Parameters { get { return this._parameters; } }
		public Dictionary<string, HttpPostFile> Files { get { return this._files; } }

		public HttpPostRequest(HttpListenerRequest request) {
			string[] contentTypeParts = request.ContentType.Split(';');
			foreach(string contentTypePart in contentTypeParts) {
				Match contentTypePartMatch = Regex.Match(contentTypePart, "(multipart\\/form-data|application\\/x-www-form-urlencoded)", RegexOptions.Compiled);
				Match boundaryMatch = Regex.Match(contentTypePart, "boundary=(.+)", RegexOptions.Compiled);
				if (contentTypePartMatch.Success) {
					this._encType = contentTypePartMatch.Groups[1].Value;
				} else if (boundaryMatch.Success) {
					this._boundary = boundaryMatch.Groups[1].Value.Trim();
				}
			}

			//SConsole.WriteLine("EncodingType: " + this.EncodingType + "; Boundary: " + this.Boundary);
			if (this.EncodingType == "application/x-www-form-urlencoded") {
				using(StreamReader reader = new StreamReader(request.InputStream, request.ContentEncoding)) {
					string queryString = reader.ReadToEnd();
					this._parameters = HttpUtility.ParseQueryString(queryString, request.ContentEncoding);
				}
			} else if (this.EncodingType == "multipart/form-data" && this.Boundary != String.Empty) {
				long requestLength = -1, currentPosition = 0;

				try {
					requestLength = request.InputStream.Length;
				} catch(NotSupportedException) {
					//Console.WriteLine("NotSupportedException: " + e.Message);
					requestLength = request.ContentLength64;
				}

				if (requestLength == -1) {
					Console.WriteLine("ungültige RequestLength");
					return;
				} else if (requestLength == 0) {
					Console.WriteLine("RequestLength == 0");
					return;
				}

				int iByte = -1;
				byte[] buffer = new byte[requestLength];
				byte lastByte;
				UTF8Encoding enc = new UTF8Encoding();
				byte[] boundaryBytes = enc.GetBytes(this.Boundary);
				List<byte> headerBufferList = new List<byte>();
				List<byte> contentBufferList = new List<byte>();
				NameValueCollection currentHeader = new NameValueCollection();

				bool boundaryFound = false, headerStarted = false, contentStarted = false;

				iByte = request.InputStream.ReadByte();
				while(iByte != -1 && currentPosition < requestLength) {
					lastByte = Convert.ToByte(iByte);
					buffer[currentPosition] = lastByte;
					if (currentPosition + 1 >= boundaryBytes.Length) {
						if (buffer[currentPosition] == boundaryBytes[boundaryBytes.Length - 1]) {
							byte[] bytesToCompare = new byte[boundaryBytes.Length];
							Array.Copy(buffer, currentPosition - boundaryBytes.Length + 1, bytesToCompare, 0, boundaryBytes.Length);
							if (this.ByteArrayEquals(bytesToCompare, boundaryBytes)) {
								if (contentStarted) {
									if (currentHeader["name"] != null) {
										contentBufferList.RemoveAt(0);
										contentBufferList.RemoveRange(contentBufferList.Count - (boundaryBytes.Length + 3), boundaryBytes.Length + 3);
										if (currentHeader["filename"] != null) {
											byte[] contentBufferArray = contentBufferList.ToArray();
											string hash = currentHeader["filename"];
											using(SHA1CryptoServiceProvider sha1 = new SHA1CryptoServiceProvider()) {
												hash = Convert.ToBase64String(sha1.ComputeHash(contentBufferArray));
											}
											string tempFilename = "/tmp/" + hash;
											Match filenameMatch = Regex.Match(currentHeader["filename"], "\\.([^\\.]+)$", RegexOptions.Compiled);
											if (filenameMatch.Success) {
												tempFilename += "." + filenameMatch.Groups[1].Value;
											}

											File.WriteAllBytes(tempFilename, contentBufferArray);
											if (File.Exists(tempFilename)) {
												FileInfo fileInfo = new FileInfo(tempFilename);
												string contentType = (currentHeader["Content-Type"] != null) ? currentHeader["Content-Type"] : "";
												HttpPostFile postFile = new HttpPostFile(currentHeader["filename"], tempFilename, fileInfo.Length, contentType);
												this._files.Add(currentHeader["name"], postFile);
											}
										} else {
											this._parameters.Add(currentHeader["name"], enc.GetString(contentBufferList.ToArray()));
										}
									}
								}

								headerBufferList.Clear();
								contentBufferList.Clear();
								boundaryFound = true;
								headerStarted = false;
								contentStarted = false;
							}
						}
					}

					if (lastByte == '\n' && currentPosition > 0 && buffer[currentPosition - 1] == '\r') {
						if (boundaryFound && !headerStarted) {
							headerBufferList.Clear();
							currentHeader.Clear();
							headerStarted = true;
						}
						if (!contentStarted && currentPosition > 3 && buffer[currentPosition - 3] == '\r' && buffer[currentPosition - 2] == '\n') {
							string[] headerStringParts = enc.GetString(headerBufferList.ToArray()).Trim().Split('\n');
							foreach(string headerString in headerStringParts) {
								string[] splittedHeaderString = headerString.Split(';');
								foreach(string splittedHeader in splittedHeaderString) {
									string currentHeaderInfo = splittedHeader.Trim();
									if (currentHeaderInfo != String.Empty) {
										Match headerContentInfoMatch = Regex.Match(currentHeaderInfo, "([^:]+):\\s*([^\\s]+)", RegexOptions.Compiled);
										if (headerContentInfoMatch.Success) {
											currentHeader.Add(headerContentInfoMatch.Groups[1].Value, headerContentInfoMatch.Groups[2].Value);
										} else {
											Match headerNameValueMatch = Regex.Match(currentHeaderInfo, "([^=]+)=\"([^\"]+)\"", RegexOptions.Compiled);
											if (headerNameValueMatch.Success) {
												currentHeader.Add(headerNameValueMatch.Groups[1].Value, headerNameValueMatch.Groups[2].Value);
											}
										}
									}
								}
							}

							headerBufferList.Clear();
							contentStarted = true;
						}
					}

					if (boundaryFound) {
						if (contentStarted) {
							contentBufferList.Add(lastByte);
						} else if (headerStarted) {
							headerBufferList.Add(lastByte);
						}
					}

					currentPosition++;
					iByte = request.InputStream.ReadByte();
				}
			}
		}

		public void Dispose() {
			this._parameters.Clear();
			foreach(KeyValuePair<string, HttpPostFile> file in this._files) {
				if (File.Exists(file.Value.TempName)) {
					File.Delete(file.Value.TempName);
				}
			}
			this._files.Clear();
		}

		private bool ByteArrayEquals(byte[] source, byte[] destination) {
			if (source.Length != destination.Length) {
				return false;
			}

			for(int i = 0; i < source.Length; i++) {
				if (source[i] != destination[i]) {
					return false;
				}
			}

			return true;
		}
	}
}
