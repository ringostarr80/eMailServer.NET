using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace TcpRequestHandler {
	public class FetchFields {
		private bool _body = false;
		private bool _bodyPeek = false;
		private bool _bodyMessage = false;
		private bool _header = false;
		private bool _headerFields = false;
		private bool _uid = false;
		private bool _rfc822Size = false;
		private bool _flags = false;
		private List<string> _fieldList = new List<string>();
		private List<string> _headerFieldList = new List<string>();
		
		public bool Body { get { return this._body; } }
		public bool BodyPeek { get { return this._bodyPeek; } }
		public bool BodyMessage { get { return this._bodyMessage; } }
		public bool Header { get { return this._header; } }
		public bool HeaderFields { get { return this._headerFields; } }
		public bool UID { get { return this._uid; } }
		public bool RFC822Size { get { return this._rfc822Size; } }
		public bool Flags { get { return this._flags; } }
		public List<string> FieldList { get { return this._fieldList; } }
		public List<string> HeaderFieldList { get { return this._headerFieldList; } }
		
		public FetchFields() {
			
		}
		
		public FetchFields(string fetch) {
			this.Parse(fetch);
		}
		
		// UID RFC822.SIZE FLAGS BODY.PEEK[HEADER.FIELDS (From To Cc Bcc Subject Date Message-ID Priority X-Priority References Newsgroups In-Reply-To Content-Type Reply-To)]
		public void Parse(string fetch) {
			fetch = fetch.Trim();
			
			this._fieldList.Clear();
			this._headerFieldList.Clear();
			
			bool fetchFieldFound = false;
			do {
				fetchFieldFound = false;
				
				string fetchField = this.DetectNextFetchField(fetch);
				if (fetchField != String.Empty) {
					fetchFieldFound = true;
					
					switch(fetchField.ToUpper()) {
						case "UID":
							this._uid = true;
							this._fieldList.Add("UID");
							break;
							
						case "RFC822.SIZE":
							this._rfc822Size = true;
							this._fieldList.Add("RFC822.SIZE");
							break;
							
						case "FLAGS":
							this._flags = true;
							this._fieldList.Add("FLAGS");
							break;
						
						default:
							if (fetchField.ToUpper().StartsWith("BODY")) {
								this._fieldList.Add("BODY");
								Match bodyPeekMatch = Regex.Match(fetch, @"BODY(.PEEK)?\[(HEADER(.FIELDS\s+)?\(([^\)]+)?\))?\]", RegexOptions.IgnoreCase);
								if (bodyPeekMatch.Success) {
									this._body = true;
									if (bodyPeekMatch.Groups[1].Value.ToUpper() == ".PEEK") {
										this._bodyPeek = true;
									}
									
									if (bodyPeekMatch.Groups[2].Value == String.Empty) {
										this._header = true;
										this._bodyMessage = true;
										this._headerFieldList.Add("Date");
										this._headerFieldList.Add("From");
										this._headerFieldList.Add("To");
										this._headerFieldList.Add("Cc");
										this._headerFieldList.Add("Bcc");
										this._headerFieldList.Add("Subject");
										this._headerFieldList.Add("Reply-To");
										this._headerFieldList.Add("In-Reply-To");
										this._headerFieldList.Add("Content-Type");
									}
									
									if (bodyPeekMatch.Groups[3].Value.Trim() != String.Empty) {
										this._header = true;
										if (bodyPeekMatch.Groups[3].Value.Trim().ToUpper() == ".FIELDS") {
											this._headerFields = true;
											if (bodyPeekMatch.Groups[4].Value != String.Empty) {
												string[] headerFields = bodyPeekMatch.Groups[4].Value.Split(new char[] {' '});
												foreach(string headerField in headerFields) {
													if (headerField.Trim() != String.Empty) {
														this._headerFieldList.Add(headerField.Trim());
													}
												}
											}
										}
									}
								}
							}
							break;
					}
					
					fetch = fetch.Substring(fetchField.Length, fetch.Length - fetchField.Length).Trim();
				}
			} while(fetchFieldFound);
		}
		
		private string DetectNextFetchField(string fetch) {
			fetch = fetch.Trim();
			
			string fetchField = String.Empty;
			int spaceIndex = fetch.IndexOf(" ");
			if (spaceIndex != -1) {
				if (spaceIndex > 0) {
					fetchField = fetch.Substring(0, spaceIndex);
				}
			} else if (fetch != String.Empty) {
				fetchField = fetch;
			}
			
			return fetchField;
		}
	}
}

