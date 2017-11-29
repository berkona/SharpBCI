using NUnit.Framework;
using System;

namespace SharpBCI.Tests {
	
	[SetUpFixture]
	public class Setup {
		[SetUp]
		public void SetupLogger() {
			Logger.AddLogOutput(new ConsoleLogger());
		}
	}

}
