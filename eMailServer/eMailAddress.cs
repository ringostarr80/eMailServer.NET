using System;

namespace eMailServer {
	public class eMailAddress {
		private string _name = String.Empty;
		private string _address = String.Empty;

		public string Name { get { return this._name; } set { this._name = value; } }

		public string Address {
			get {
				return this._address;
			}
			set {
				RegexUtilities regexUtility = new RegexUtilities();
				if (regexUtility.IsValidEmail(value)) {
					this._address = value;
				} else {
					throw new FormatException("invalid eMail address format => \"" + value + "\"");
				}
			}
		}

		public eMailAddress() {

		}

		public eMailAddress(string name, string address) {
			this.Name = name;
			this.Address = address;
		}
	}
}

