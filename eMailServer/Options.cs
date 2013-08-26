using System;
using CommandLine;
using CommandLine.Text;

namespace eMailServer {
	public class Options {
		[Option('h', "http-port", DefaultValue = 80, Required = false, HelpText = "The HTTP-Port to listen.")]
		public int HttpPort { get; set; }
		
		[Option('d', "disable-http-server", DefaultValue = false, Required = false, HelpText = "Disable the HTTP-Server.")]
		public bool DisableHttpServer { get; set; }

		[Option('s', "smtp-port", DefaultValue = 25, Required = false, HelpText = "The SMTP-Port to listen.")]
		public int SmtpPort { get; set; }
		
		[Option('t', "disable-smtp-server", DefaultValue = false, Required = false, HelpText = "Disable the SMTP-Server.")]
		public bool DisableSmtpServer { get; set; }

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

