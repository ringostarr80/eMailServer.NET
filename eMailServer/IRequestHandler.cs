using System;

namespace eMailServer {
	public interface IRequestHandler {
		void ProcessRequest();
		void OutputResult();
	}
}

