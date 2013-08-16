using System;

namespace HttpPostRequest {
	public class HttpPostFile {
		private long _length = 0;
		private string _name = String.Empty;
		private string _tempName = String.Empty;
		private string _contentType = String.Empty;

		public long Length { get { return this._length; } }
		public string Name { get { return this._name; } }
		public string TempName { get { return this._tempName; } }
		public string ContentType { get { return this._contentType; } }

		public HttpPostFile(string name, string tempName, long length) {
			this._name = name;
			this._tempName = tempName;
			this._length = length;
		}

		public HttpPostFile(string name, string tempName, long length, string contentType) {
			this._name = name;
			this._tempName = tempName;
			this._length = length;
			this._contentType = contentType;
		}
	}
}

