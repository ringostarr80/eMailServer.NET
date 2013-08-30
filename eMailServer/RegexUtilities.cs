using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace eMailServer {
	public class RegexUtilities {
		private bool _invalid = false;

		public RegexUtilities() {

		}

		public bool IsValidEmail(string strIn) {
			this._invalid = false;
			if (String.IsNullOrEmpty(strIn)) {
				return false;
			}

			// Use IdnMapping class to convert Unicode domain names. 
			strIn = Regex.Replace(strIn, @"(@)(.+)$", this.DomainMapper, RegexOptions.None);

			if (this._invalid) { 
				return false;
			}

			// Return true if strIn is in valid e-mail format.
			return Regex.IsMatch(strIn, @"^(?("")(""[^""]+?""@)|(([0-9a-z]((\.(?!\.))|[-!#\$%&'\*\+/=\?\^`\{\}\|~\w])*)(?<=[0-9a-z])@))(?(\[)(\[(\d{1,3}\.){3}\d{1,3}\])|(([0-9a-z][-\w]*[0-9a-z]*\.)+[a-z0-9]{2,17}))$", RegexOptions.IgnoreCase);
		}

		private string DomainMapper(Match match) {
			// IdnMapping class with default property values.
			IdnMapping idn = new IdnMapping();

			string domainName = match.Groups[2].Value;
			try {
				domainName = idn.GetAscii(domainName);
			} catch(ArgumentException) {
				this._invalid = true;      
			}      
			return match.Groups[1].Value + domainName;
		}
	}
}

