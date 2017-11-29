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

			Assert.DoesNotThrow(() => new TournamentArtifactDectector(1, 1, 1, 1));
		}

		[Test]
		public void DetectNormal() {
			var model = new ARModel(0, new double[] { 0.5 });
			var detector = new TournamentArtifactDectector(1, 1000, 1, 100);
			var r = new Random(12345);
			var last = r.NextDouble();
			// prime detector
			for (int i = 0; i < 1005; i++) {
				detector.Detect(last);
				last = model.Predict(last) + r.NextDouble();
			}

			//last = model.Predict(last) + r.NextDouble();
			for (int i = 0; i < 10; i++) {
				Assert.False(detector.Detect(last));
				last = model.Predict(last) + r.NextDouble();
			}
		}

		[Test]
		public void DetectArtifact() {
			var model = new ARModel(0, new double[] { 0.5 });
			var detector = new TournamentArtifactDectector(1, 1000, 1, 1);
			var r = new Random(12345);
			var last = 10 * r.NextDouble();
			// extra 2 to ensure underlying ARArtifactDetector is primed
			for (int i = 0; i < 1002; i++) {
				Assert.False(detector.Detect(last));
				last = model.Predict(last) + 10 * r.NextDouble();
			}

			// high-amplitude artifact for 100 samples
			for (int i = 0; i < 100; i++) {
				Assert.True(detector.Detect(last + 1000));
			}
		}

		[Test]
		public void DetectModelChange() {
			// change underlying model and see if it adapts
			var model = new ARModel(0, new double[] { 0.5 });
			var detector = new TournamentArtifactDectector(3, 1000, 1, 1);
			var r = new Random(12345);
			var last = 10 * r.NextDouble();
			// extra 2 to ensure underlying ARArtifactDetector is primed
			for (int i = 0; i< 1002; i++) {
				Assert.False(detector.Detect(last));
				last = model.Predict(last) + 10 * r.NextDouble();
			}

			model = new ARModel(100, new double[] { 0.5 });
			for (int i = 0; i < 1000; i++) {
				detector.Detect(last);
				last = model.Predict(last) + 10 * r.NextDouble();
			}

			for (int i = 0; i < 100; i++) {
				Assert.False(detector.Detect(last));
				last = model.Predict(last) + 10 * r.NextDouble();
			}
		}
	}
}
