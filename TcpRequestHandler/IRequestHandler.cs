using System;

namespace TcpRequestHandler {
	public interface IRequestHandler {
		event TcpRequestEventHandler Connected;
		event TcpRequestEventHandler Disconnected;
		
		void Start();
		void OutputResult();
		void WaitForClosing();
	}
}

