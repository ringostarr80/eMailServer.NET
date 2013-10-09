using System;
using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Driver;

namespace eMailServer {
	public class eMailEntity {
		public ObjectId Id { get; set; }
		public string ClientName { get; set; }
		public DateTime Time { get; set; }
		public string MailFrom { get; set; }
		public string RecipientTo { get; set; }
		public string Subject { get; set; }
		public string Message { get; set; }
		public string Folder { get; set; }
		public eMailAddress HeaderFrom { get; set; }
		public eMailAddress HeaderReplyTo { get; set; }
		public List<eMailAddress> HeaderTo { get; set; }
		public List<eMailAddress> HeaderCc { get; set; }
		public DateTime HeaderDate { get; set; }
		public List<string> Flags { get; set; }
		public List<KeyValuePair<string, string>> RawHeader { get; set; }
	}
}
