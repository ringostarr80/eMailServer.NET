using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;
using CommandLine.Text;
using MongoDB.Bson;
using MongoDB.Driver;
using NLog;
using TcpRequestHandler;

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
			
			MongoServer mongoServer = MyMongoDB.GetServer();
			try {
				mongoServer.Connect();
			} catch(MongoConnectionException e) {
				Console.WriteLine("MongoConnectionException: " + e.Message);
				LogManager.Configuration = null;
				return;
			}
			
			if (Options.Check) {
				Check();
			}
			
			HttpListener httpListener = null;
			TcpListener smtpListener = null;
			TcpListener secureSmtpListener = null;
			TcpListener imapListener = null;
			TcpListener secureImapListener = null;
			
			if (Options.ServerCertificateFilename != String.Empty) {
				if (File.Exists(Options.ServerCertificateFilename)) {
					try {
						TcpRequestHandler.TcpRequestHandler.SetServerCertificate(Options.ServerCertificateFilename);
					} catch(Exception ex) {
						logger.ErrorException("Certificate Exception message: " + ex.Message, ex);
						LogManager.Configuration = null;
						return;
					}
				} else {
					logger.Error("Can't find certificate file: " + Options.ServerCertificateFilename);
					LogManager.Configuration = null;
					return;
				}
			}

			if (Options.ServerKeyFilename != String.Empty) {
				if (File.Exists(Options.ServerKeyFilename)) {
					try {
						TcpRequestHandler.TcpRequestHandler.SetServerKeyFile(Options.ServerKeyFilename);
					} catch(Exception ex) {
						logger.ErrorException("Server Key-File Exception message: " + ex.Message, ex);
						LogManager.Configuration = null;
						return;
					}
				} else {
					logger.Error("Can't find server key file file: " + Options.ServerKeyFilename);
					LogManager.Configuration = null;
					return;
				}
			}
			
			LimitedConcurrencyLevelTaskScheduler taskScheduler = new LimitedConcurrencyLevelTaskScheduler(500);
			TaskFactory factory = new TaskFactory(taskScheduler);
			
			if (!Options.DisableHttpServer) {
				if (!HttpListener.IsSupported) {
					logger.Error("HttpListener is not supported by this System!");
					LogManager.Configuration = null;
					return;
				}
				string[] prefixes = {"http://*:" + Options.HttpPort + "/"};
				httpListener = new HttpListener();
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
			} else {
				((AutoResetEvent)waitHandles[0]).Set();
			}
			
			if (!Options.DisableSmtpServer) {
				smtpListener = new TcpListener(IPAddress.Any, Options.SmtpPort);
				secureSmtpListener = new TcpListener(IPAddress.Any, Options.SecureSmtpPort);
				imapListener = new TcpListener(IPAddress.Any, Options.ImapPort);
				secureImapListener = new TcpListener(IPAddress.Any, Options.SecureImapPort);
	
				try {
					smtpListener.Start();
					secureSmtpListener.Start();
					imapListener.Start();
					secureImapListener.Start();
				} catch(Exception e) {
					logger.Error("TcpListener: " + e.Message);
					LogManager.Configuration = null;
					return;
				}
	
				logger.Info("Listening on SMTP-Port " + Options.SmtpPort);
				logger.Info("Listening on Secure SMTP-Port " + Options.SecureSmtpPort);
				logger.Info("Listening on IMAP-Port " + Options.ImapPort);
				logger.Info("Listening on Secure IMAP-Port " + Options.SecureImapPort);

				Action<object> receiveRequest = (object listener) => {
					Thread.CurrentThread.CurrentCulture = CultureInfo.CreateSpecificCulture("en-US");
					bool repeatSmtpListener = true;
					do {
						try {
							factory.StartNew((context) => {
								Thread.CurrentThread.CurrentCulture = CultureInfo.CreateSpecificCulture("en-US");

								TcpClient tcpClient = (TcpClient)context;
								IPEndPoint endPoint = (IPEndPoint)tcpClient.Client.LocalEndPoint;
								IRequestHandler handler;
								if (endPoint.Port != Options.ImapPort && endPoint.Port != Options.SecureImapPort) {
									handler = new SmtpRequestHandler(tcpClient);
								} else {
									handler = new ImapServer(tcpClient, Options.SecureImapPort);
								}

								try {
									handler.Start();
									handler.WaitForClosing();
								} catch(Exception e) {
									logger.ErrorException(e.Message, e);
									logger.Error(e.StackTrace);
								} finally {
									handler.OutputResult();
								}
							}, (object)((TcpListener)listener).AcceptTcpClient(), TaskCreationOptions.PreferFairness);
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
				};
	
				// Smtp-Listener Task
				factory.StartNew(receiveRequest, smtpListener);
				factory.StartNew(receiveRequest, secureSmtpListener);
				factory.StartNew(receiveRequest, imapListener);
				factory.StartNew(receiveRequest, secureImapListener);
			} else {
				((AutoResetEvent)waitHandles[1]).Set();
			}

			WaitHandle.WaitAll(waitHandles);
			
			if (!Options.DisableHttpServer) {
				if (httpListener != null) {
					httpListener.Close();
				}
			}
			if (!Options.DisableSmtpServer) {
				if (smtpListener != null) {
					smtpListener.Stop();
				}
				if (secureSmtpListener != null) {
					secureSmtpListener.Stop();
				}
				if (imapListener != null) {
					imapListener.Stop();
				}
				if (secureImapListener != null) {
					secureImapListener.Stop();
				}
			}
			
			if (Options.DisableHttpServer && Options.DisableSmtpServer) {
				Console.WriteLine("Invalid Parameter Combination: You have disabled the HTTP-Server and the SMTP-Server.");
			}
			
			LogManager.Configuration = null;
		}
		
		private static void Check() {
			Console.WriteLine("running precheck...");
			
			MongoServer mongoServer = MyMongoDB.GetServer();
			
			MongoDatabase mongoDatabase = mongoServer.GetDatabase("email");
			MongoCollection<eMailEntity> mongoCollection = mongoDatabase.GetCollection<eMailEntity>("mails");
			
			MongoCursor<eMailEntity> mongoCursor = mongoCollection.FindAll();
			foreach(eMailEntity entity in mongoCursor) {
				if (User.EMailExists(entity.RecipientTo)) {
					Console.WriteLine("user with email-address found: " + entity.RecipientTo);
					User newMailUser = new User();
					newMailUser.RefreshById(User.GetIdByEMail(entity.RecipientTo));
					eMail mail = new eMail(entity);
					mail.AssignToUser(newMailUser);
				}
			}
			
			Console.WriteLine("precheck finished...");
		}
	}
}
