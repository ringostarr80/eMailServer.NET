using System;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using NLog;

namespace eMailServer {
	public class HttpRequestHandler {
		private static Logger logger = LogManager.GetCurrentClassLogger();

		private XmlDocument _doc;
		private HttpListenerResponse _response = null;
		private HttpListenerRequest _request = null;
		private string _xmlRoot = "email_server";

		public string RequestUrl { get; set; }
		public string RequestRawUrl { get; set; }
		public NameValueCollection RequestQueryString { get; set; }
		public NameValueCollection RequestHeaders { get; set; }
		public Stream ResponseOutputStream { get; set; }
		public HttpListenerResponse Response {
			get {
				return this._response;
			}
			set {
				this._response = value;
				this.ResponseOutputStream = this._response.OutputStream;
			}
		}
		/// <summary>
		/// Gets or sets the request.
		/// </summary>
		/// <value>
		/// The request.
		/// </value>
		public HttpListenerRequest Request {
			get {
				return this._request;
			}
			set {
				this._request = value;
				this.RequestUrl = this._request.Url.ToString();
				this.RequestRawUrl = this._request.RawUrl;
				this.RequestQueryString = this._request.QueryString;
				this.RequestHeaders = this._request.Headers;
			}
		}

		public HttpRequestHandler() {
			this._doc = new XmlDocument();
			this._doc.CreateXmlDeclaration("1.0", "utf-8", "yes");
			this._doc.AppendChild(this._doc.CreateElement(this._xmlRoot));
		}

		public HttpRequestHandler(HttpListenerContext context) {
			this.Request = context.Request;
			this.Response = context.Response;
			
			this._doc = new XmlDocument();
			this._doc.CreateXmlDeclaration("1.0", "utf-8", "yes");
			this._doc.AppendChild(this._doc.CreateElement(this._xmlRoot));
		}

		public void ProcessRequest() {
			string RequestType = "";
			
			logger.Trace("Url: " + this.RequestUrl);
			XmlNode XmlRoot = this._doc.GetElementsByTagName(this._xmlRoot).Item(0);
			
			XmlElement XmlRequest = this._doc.CreateElement("request");
			string UrlPath = Regex.Replace(this.RequestRawUrl, "[^\\/]*$", "");
			Match TypeMatch = Regex.Match(UrlPath, "^\\/([^\\/]+)\\/");
			if (TypeMatch.Success) {
				RequestType = TypeMatch.Groups[1].Value;
			}
			XmlRequest.SetAttribute("type", RequestType);
			XmlRequest.SetAttribute("url", this.RequestUrl);
			XmlRequest.SetAttribute("url_path", UrlPath);
			XmlElement XmlParams = this._doc.CreateElement("params");
			for(int i = 0; i < this.RequestQueryString.Count; i++) {
				XmlElement XmlParam = this._doc.CreateElement("param");
				XmlParam.SetAttribute("name", this.RequestQueryString.GetKey(i));
				XmlParam.SetAttribute("value", this.RequestQueryString[i].ToString());
				XmlParams.AppendChild(XmlParam);
			}
			XmlRequest.AppendChild(XmlParams);
			XmlRoot.AppendChild(XmlRequest);
			
			bool accessAllowed = false;
			if (this.Request.UrlReferrer != null) {
				Match referrerMatch = Regex.Match(this.Request.UrlReferrer.Host, "^(localhost|127\\.0\\.0\\.1|[^\\.]+\\.locrmaps\\.com|[^\\.]+\\.locr\\.com)$");
				if (referrerMatch.Success) {
					accessAllowed = true;
				}
			}
			if (!accessAllowed) {
				if (this.Request.Url.Host == "localhost" || this.Request.Url.Host == "127.0.0.1") {
					accessAllowed = true;
				} else {
					/*
					Options options = eMailServer.Options;
					if (options.AllowedRemoteAddresses != String.Empty) {
						string remoteAddress = this.Request.RemoteEndPoint.Address.ToString();
						string[] allowedRemoteAddresses = options.AllowedRemoteAddresses.Split(',');
						foreach(string allowedRemoteAddress in allowedRemoteAddresses) {
							if (allowedRemoteAddress == remoteAddress) {
								accessAllowed = true;
								break;
							} else if (allowedRemoteAddress.Contains("*")) {
								Match allowedRemoteAddressWildcardMatch = Regex.Match(allowedRemoteAddress, "^([0-9]+|\\*)\\.([0-9]+|\\*)\\.([0-9]+|\\*)\\.([0-9]+|\\*)$", RegexOptions.Compiled);
								if (allowedRemoteAddressWildcardMatch.Success) {
									Match remoteAddressMatch = Regex.Match(remoteAddress, "^([0-9]+)\\.([0-9]+)\\.([0-9]+)\\.([0-9]+)$", RegexOptions.Compiled);
									if (remoteAddressMatch.Success) {
										if ((allowedRemoteAddressWildcardMatch.Groups[1].Value != "*" && remoteAddressMatch.Groups[1].Value == allowedRemoteAddressWildcardMatch.Groups[1].Value || allowedRemoteAddressWildcardMatch.Groups[1].Value == "*") &&
											(allowedRemoteAddressWildcardMatch.Groups[2].Value != "*" && remoteAddressMatch.Groups[2].Value == allowedRemoteAddressWildcardMatch.Groups[2].Value || allowedRemoteAddressWildcardMatch.Groups[2].Value == "*") &&
											(allowedRemoteAddressWildcardMatch.Groups[3].Value != "*" && remoteAddressMatch.Groups[3].Value == allowedRemoteAddressWildcardMatch.Groups[3].Value || allowedRemoteAddressWildcardMatch.Groups[3].Value == "*") &&
											(allowedRemoteAddressWildcardMatch.Groups[4].Value != "*" && remoteAddressMatch.Groups[4].Value == allowedRemoteAddressWildcardMatch.Groups[4].Value || allowedRemoteAddressWildcardMatch.Groups[4].Value == "*")) {
											accessAllowed = true;
										}
									}
								}
							}
						}
						//accessAllowed = allowedRemoteAddresses.Any(a => a == remoteAddress);
					}
					*/
				}
			}
			
			if (accessAllowed) {
				switch(RequestType) {
					case "files":
						this.OutputFile(this.RequestRawUrl);
						break;
					case "":
						this.OutputFile("/files/index.html");
						break;
					default:
						//Console.WriteLine("Error: Invalid RequestType = " + RequestType);
						break;
				}
			} else {
				XmlElement XmlError = this._doc.CreateElement("error");
				XmlError.SetAttribute("code", ((int)HttpStatusCode.Unauthorized).ToString());
				XmlError.SetAttribute("message", "access denied");
				XmlError.SetAttribute("remote_address", this.Request.RemoteEndPoint.Address.ToString());
				XmlRoot.AppendChild(XmlError);
			}
		}

		private void OutputFile(string file) {
			logger.Trace("OutputFile(" + file + ")");
			
			this.Response.AddHeader("Last-Modified", eMailServer.StartTime.ToString("R"));
			
			string IfNoneMatch = "";
			if (this.RequestHeaders["If-None-Match"] != null && this.RequestHeaders["If-None-Match"] != "") {
				IfNoneMatch = this.RequestHeaders["If-None-Match"];
			}
			
			NameValueCollection FileEtags = eMailServer.FileEtags;
			if (FileEtags[file] != null && FileEtags[file] != "" && "\"" + FileEtags[file] + "\"" == IfNoneMatch) {
				this.Response.StatusCode = (int)HttpStatusCode.NotModified;
				this.Response.AddHeader("Etag", "\"" + FileEtags[file] + "\"");
				this.ResponseOutputStream.Close();
			} else {
				Assembly assembly = Assembly.GetExecutingAssembly();
				try {
					Stream stream = assembly.GetManifestResourceStream("eMailServer" + file.Replace("/", "."));
					
					if (stream.CanSeek) {
						MD5 md5 = new MD5CryptoServiceProvider();
						FileEtags[file] = System.BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", "").ToLower();
						this.Response.AddHeader("Etag", "\"" + FileEtags[file] + "\"");
						stream.Position = 0;
					}
					
					this.Response.ContentLength64 = stream.Length;
					
					Match ExtMatch = Regex.Match(file, "([^\\.]+)$");
					if (ExtMatch.Success) {
						switch(ExtMatch.Groups[1].Value) {
							case "html":
							case "xml":
							case "xsl":
							case "js":
							case "css":
								this.Response.ContentEncoding = Encoding.UTF8;
								break;
							case "gif":
							case "png":
							case "jpg":
							case "jpeg":
								this.Response.AppendHeader("Accept-Range", "bytes");
								break;
						}
						
						switch(ExtMatch.Groups[1].Value) {
							case "html":
								this.Response.ContentType = "text/html";
								break;
							case "xml":
								this.Response.ContentType = "text/xml";
								break;
							case "xsl":
								this.Response.ContentType = "text/xsl";
								break;
							case "js":
								this.Response.ContentType = "application/javascript";
								break;
							case "css":
								this.Response.ContentType = "text/css";
								break;
							case "gif":
								this.Response.ContentType = "image/gif";
								break;
							case "png":
								this.Response.ContentType = "image/png";
								break;
							case "jpg":
							case "jpeg":
								this.Response.ContentType = "image/jpeg";
								break;
						}
					}
					
					Byte[] data = new Byte[stream.Length];
					stream.Read(data, 0, (int)stream.Length);
					this.ResponseOutputStream.Write(data, 0, (int)stream.Length);
				} catch(Exception e) {
					this.Response.StatusCode = (int)HttpStatusCode.NotFound;
					logger.Error(e.Message);
				} finally {
					this.ResponseOutputStream.Close();
				}
			}
		}

		public void OutputResult() {
			StringBuilder sb = new StringBuilder();
			this._doc.Save(new StringWriter(sb));
			
			try {
				byte[] buffer = Encoding.UTF8.GetBytes(sb.ToString());
				
				if (this.Response != null) {
					this.Response.ContentEncoding = Encoding.UTF8;
					this.Response.ContentType = "text/xml";
					this.Response.ContentLength64 = buffer.Length;
				}
				
				this.ResponseOutputStream.Write(buffer, 0, buffer.Length);
				this.ResponseOutputStream.Close();
			} catch(Exception e) {
				logger.Trace(e.Message);
			}
		}
	}
}

