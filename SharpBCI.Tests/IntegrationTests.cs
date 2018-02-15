using NUnit.Framework;
using System;
using SharpBCI;

namespace SharpBCI.Tests {

	[TestFixture]
	public class IntegrationTests {

		[Test]
		public void ConstructionFromFileTest() {
			var museAdapter = new RemoteOSCAdapter(5000);
			var sharpBCI = new SharpBCIBuilder()
				.EEGDeviceAdapter(museAdapter)
				.PipelineFile("../../ConstructionTestConfig.json")
				.Build();

			sharpBCI.Close();
		}
	}

}