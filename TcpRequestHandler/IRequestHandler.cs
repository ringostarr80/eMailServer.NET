using System;

namespace TcpRequestHandler {
	public interface IRequestHandler {
		void ProcessRequest();
		void OutputResult();
	}
}

