using System;
using System.Collections.Specialized;
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
		public static NameValueCollection FileEtags = new NameValueCollection();

		public static void Main(string[] args) {
			Thread.CurrentThread.CurrentCulture = CultureInfo.CreateSpecificCulture("en-US");
			WaitHandle[] waitHandles = new WaitHandle[] {
				new AutoResetEvent(false),
				new AutoResetEvent(false)
			};

			Parser parser = Parser.Default;
			if (!parser.ParseArguments(args, Options)) {
				LogManager.Configuration = null;
				return;
			}

			if (!HttpListener.IsSupported) {
				logger.Error("HttpListener is not supported by this System!");
				LogManager.Configuration = null;
				return;
			}
			string[] prefixes = {"http://*:" + Options.HttpPort + "/"};
			HttpListener httpListener = new HttpListener();
			foreach(string s in prefixes) {
				httpListener.Prefixes.Add(s);
			}

			try {
				httpListener.Start();
			} catch(Exception e) {
				logger.Error("HttpListener: " + e.Message);
				LogManager.Configuration = null;
				return;
			}

			logger.Info("Listening on HTTP-Port " + Options.HttpPort);

			TcpListener smtpListener = new TcpListener(IPAddress.Parse("127.0.0.1"), Options.SmtpPort);

			try {
				smtpListener.Start();
			} catch(Exception e) {
				logger.Error("TcpListener: " + e.Message);
				LogManager.Configuration = null;
				return;
			}

			logger.Info("Listening on SMTP-Port " + Options.SmtpPort);

			LimitedConcurrencyLevelTaskScheduler taskScheduler = new LimitedConcurrencyLevelTaskScheduler(500);
			TaskFactory factory = new TaskFactory(taskScheduler);

			// Http-Listener Task
			factory.StartNew(() => {
				Thread.CurrentThread.CurrentCulture = CultureInfo.CreateSpecificCulture("en-US");
				bool repeatHttpListener = true;
				do {
					try {
						factory.StartNew((context) => {
							Thread.CurrentThread.CurrentCulture = CultureInfo.CreateSpecificCulture("en-US");
							HttpRequestHandler handler = new HttpRequestHandler((HttpListenerContext)context);
							try {
								handler.ProcessRequest();
							} catch(Exception e) {
								logger.ErrorException(e.Message, e);
								logger.Error(e.StackTrace);
							} finally {
								handler.OutputResult();
							}
						}, (object)httpListener.GetContext(), TaskCreationOptions.PreferFairness);
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
				} while(repeatHttpListener);

				((AutoResetEvent)waitHandles[0]).Set();
			}
			);

			// Smtp-Listener Task
			factory.StartNew(() => {
				Thread.CurrentThread.CurrentCulture = CultureInfo.CreateSpecificCulture("en-US");
				bool repeatSmtpListener = true;
				do {
					try {
						factory.StartNew((context) => {
							Thread.CurrentThread.CurrentCulture = CultureInfo.CreateSpecificCulture("en-US");
							SmtpRequestHandler handler = new SmtpRequestHandler((TcpClient)context);
							try {
								handler.ProcessRequest();
							} catch(Exception e) {
								logger.ErrorException(e.Message, e);
								logger.Error(e.StackTrace);
							} finally {
								handler.OutputResult();
							}
						}, (object)smtpListener.AcceptTcpClient(), TaskCreationOptions.PreferFairness);
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
				} while(repeatSmtpListener);

				((AutoResetEvent)waitHandles[1]).Set();
			}
			);

			WaitHandle.WaitAll(waitHandles);

			httpListener.Close();
			smtpListener.Stop();
			LogManager.Configuration = null;
		}
	}
}