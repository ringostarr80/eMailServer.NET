using System;
using CommandLine;
using CommandLine.Text;

namespace eMailServer {
	public class Options {
		[Option('p', "port", DefaultValue = 25, Required = false, HelpText = "The SMTP-Port to listen.")]
		public int Port { get; set; }

		public Options() {

		}

		[HelpOption(HelpText = "Display this help screen.")]
		public string GetUsage() {
			HelpText help = new HelpText(eMailServer.HeadingInfo);
			help.AdditionalNewLineAfterOption = true;
			help.Copyright = new CopyrightInfo("Ringo Leese", 2013, 2013);
			help.AddOptions(this);
				
			return help;
		}
	}
}

