using NUnit.Framework;

using System;
using System.Threading;

using SharpOSC;

namespace SharpBCI.Tests {
	
	[TestFixture]
	public class RemoteOSCAdapterTesting {

		bool messageReceived = false;
		double[] connectStatus;

		[Test]
		public void TestDeviceHangup() {
			// setup
			var sender = new UDPSender("127.0.0.1", 5000);
			var adapter = new RemoteOSCAdapter(5000);
			adapter.AddHandler(EEGDataType.CONTACT_QUALITY, HandleConnStatus);
			adapter.Start();

			// test that it recieves our packet
			var packet = new OscMessage("/muse/elements/horseshoe", new object[] { 1, 1, 1, 1});
			sender.Send(packet);
			while (!messageReceived)
				adapter.FlushEvents();
			CheckConnStatus(new double[] { 1, 1, 1, 1 });

			// test hangup
			Thread.Sleep(RemoteOSCAdapter.HANGUP_TIME * 2);
			adapter.FlushEvents();
			CheckConnStatus(new double[] { 4, 4, 4, 4 });

			// teardown
			adapter.Stop();
		}

		void CheckConnStatus(double[] expected) {
			Assert.AreEqual(expected.Length, connectStatus.Length);
			for (int i = 0; i < expected.Length; i++) {
				Assert.LessOrEqual(Math.Abs(expected[i] - connectStatus[i]), 1e-3, "Expected connection status of {0}, but was {1}", expected[i], connectStatus[i]);
			}
		}

		void HandleConnStatus(EEGEvent evt) {
			messageReceived = true;
			connectStatus = evt.data;
		}
	}
}