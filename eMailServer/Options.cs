using System;
using CommandLine;
using CommandLine.Text;

namespace eMailServer {
	/// <summary>
	/// CommandLine Options.
	/// </summary>
	/// <see cref="http://commandline.codeplex.com/"/>
	public class Options {
		[Option('a', "db-address", DefaultValue = "localhost", Required = false, HelpText = "The database address where to connect.")]
		public string DatabaseAddress { get; set; }
		
		[Option('c', "check", DefaultValue = false, Required = false, HelpText = "run a precheck, that reassigns undelivered emails, if possible.")]
		public bool Check { get; set; }
		
		[Option('d', "disable-http-server", DefaultValue = false, Required = false, HelpText = "Disable the HTTP-Server.")]
		public bool DisableHttpServer { get; set; }
		
		[Option('e', "server-certificate-filename", DefaultValue = "", Required = false, HelpText = "Sets the filename for the SSL certificate.")]
		public string ServerCertificateFilename { get; set; }
		
		[Option('t', "disable-smtp-server", DefaultValue = false, Required = false, HelpText = "Disable the SMTP-Server.")]
		public bool DisableSmtpServer { get; set; }
		
		[Option('h', "http-port", DefaultValue = 80, Required = false, HelpText = "The HTTP-Port to listen.")]
		public int HttpPort { get; set; }

		[Option('m', "secure-smtp-port", DefaultValue = 465, Required = false, HelpText = "The Secure SMTP-Port to listen.")]
		public int SecureSmtpPort { get; set; }
		
		[Option('s', "smtp-port", DefaultValue = 25, Required = false, HelpText = "The SMTP-Port to listen.")]
		public int SmtpPort { get; set; }

		[Option('i', "imap-port", DefaultValue = 143, Required = false, HelpText = "The IMAP-Port to listen.")]
		public int ImapPort { get; set; }

		[Option('j', "secure-imap-port", DefaultValue = 993, Required = false, HelpText = "The Secure IMAP-Port to listen.")]
		public int SecureImapPort { get; set; }

		[Option('v', "verbose", DefaultValue = false, Required = false, HelpText = "Output more informations.")]
		public bool Verbose { get; set; }

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

