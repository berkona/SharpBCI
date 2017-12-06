using NUnit.Framework;
using System;

namespace SharpBCI.Tests {
	
	[TestFixture]
	public class LoggerTesting {
		
		[Test]
		public void TestLogger() {
			Logger.AddLogOutput(new FileLogger("test.txt"));
			for (int i = 0; i < 1e5; i++) {
				Logger.Log("Test");
			}
		}
	}

}