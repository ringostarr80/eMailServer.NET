using System;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;
using CommandLine.Text;
using NLog;

namespace eMailServer {
	class eMailServer {
		private static Logger logger = LogManager.GetCurrentClassLogger();
		public static HeadingInfo HeadingInfo = new HeadingInfo(".NET eMail-Server", Assembly.GetExecutingAssembly().GetName().Version.Major + "." + Assembly.GetExecutingAssembly().GetName().Version.Minor);
		public static Options Options = new Options();
		public static DateTime StartTime = DateTime.Now;

		public static void Main(string[] args) {
			Thread.CurrentThread.CurrentCulture = CultureInfo.CreateSpecificCulture("en-US");

			Parser parser = Parser.Default;
			if (!parser.ParseArguments(args, Options)) {
				LogManager.Configuration = null;
				return;
			}

			TcpListener listener = new TcpListener(IPAddress.Parse("127.0.0.1"), Options.Port);

			try {
				listener.Start();
			} catch(Exception e) {
				logger.Error("TcpListener: " + e.Message);
				LogManager.Configuration = null;
				return;
			}

			logger.Info("Listening on SMTP-Port " + Options.Port);

			LimitedConcurrencyLevelTaskScheduler taskScheduler = new LimitedConcurrencyLevelTaskScheduler(500);
			TaskFactory factory = new TaskFactory(taskScheduler);

			do {
				try {
					factory.StartNew((context) => {
						Thread.CurrentThread.CurrentCulture = CultureInfo.CreateSpecificCulture("en-US");
						RequestHandler handler = new RequestHandler((TcpClient)context);
						try {
							handler.ProcessRequest();
						} catch(Exception e) {
							logger.ErrorException(e.Message, e);
							logger.Error(e.StackTrace);
						} finally {
							handler.OutputResult();
						}
					}, (object)listener.AcceptTcpClient(), TaskCreationOptions.PreferFairness);
				} catch(AggregateException e) {
					logger.Error("Es sind " + e.InnerExceptions.Count + " Fehler aufgetreten");
					AggregateException eFlatten = e.Flatten();
					eFlatten.Handle(exc => {
						logger.Error(exc.Message);
						return true;
					}
					);
				} catch(Exception e) {
					logger.Error("Exception aufgetreten: " + e.Message);
				}
			} while(true);

			listener.Stop();
			LogManager.Configuration = null;
		}
	}
}
