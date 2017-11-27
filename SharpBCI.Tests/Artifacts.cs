using NUnit.Framework;
using System;

namespace SharpBCI.Tests {

	[TestFixture]
	public class ARArtifactDetectorTesting {

		[Test]
		public void Constructor() {
			Assert.Throws<ArgumentException>(() => new ARArtifactDetector(null));
			Assert.DoesNotThrow(() => new ARArtifactDetector(new ARModel(0, new double[] {1})));
		}

		[Test]
		public void DetectNormal() {
			var generator = new ARModel(0, new double[] { 1 });
			var arDetector = new ARArtifactDetector(generator);

			double last = 1.0;
			for (int i = 0; i < 1000; i++) {
				var next = generator.Predict(last);
				Assert.False(arDetector.Detect(next));
				last = next;
			}
		}

		[Test]
		public void DetectArtifact() {
			var generator = new ARModel(0, new double[] { 1 });
			var arDetector = new ARArtifactDetector(generator);

			double last = 1.0;
			for (int i = 0; i< 1000; i++) {
				var next = generator.Predict(last);
				Assert.False(arDetector.Detect(next));
				last = next;
			}

			var artifact = 100.0;
			Assert.True(arDetector.Detect(artifact));
		}

	}

	[TestFixture]
	public class TournamentArtifactDectectorTesting {

		[Test]
		public void Constructor() {
			Assert.Throws<ArgumentException>(() => new TournamentArtifactDectector(0, 1, 1, 1));
			Assert.Throws<ArgumentException>(() => new TournamentArtifactDectector(1, 0, 1, 1));
			Assert.Throws<ArgumentException>(() => new TournamentArtifactDectector(1, 1, 0, 1));
			Assert.Throws<ArgumentException>(() => new TournamentArtifactDectector(1, 1, 1, 0));

			// nAccept > tournamentSize
			Assert.Throws<ArgumentException>(() => new TournamentArtifactDectector(1, 1, 2, 1));
		}

	}
}
