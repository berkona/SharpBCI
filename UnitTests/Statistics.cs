using NUnit.Framework;
using System;

namespace SharpBCI.Tests {

	[TestFixture]
	public class StatsUtilTesting {

		[SetUp]
		protected void Setup() {
			Logger.AddLogOutput(new ConsoleLogger());
		}

		[Test]
		public void SampleMean() {
			// check edge cases
			Assert.Throws<ArgumentOutOfRangeException>(() => StatsUtils.SampleMean(null));
			Assert.Throws<ArgumentOutOfRangeException>(() => StatsUtils.SampleMean(new double[] { }));

			// check normal cases
			IsWithinThreshold(0, StatsUtils.SampleMean(new double[] { 0 }), 1e-5);
			IsWithinThreshold(0, StatsUtils.SampleMean(new double[] { 0, 1, -1 }), 1e-5);

			// test for significant cancellation issues
			IsWithinThreshold(0, StatsUtils.SampleMean(new double[] { 0, 1e5, -1e5 }), 1e-5);
		}

		[Test]
		public void SampleVar() {
			// check edge cases
			Assert.Throws<ArgumentOutOfRangeException>(() => StatsUtils.SampleVar(null));
			Assert.Throws<ArgumentOutOfRangeException>(() => StatsUtils.SampleVar(new double[] { }));
			Assert.Throws<ArgumentOutOfRangeException>(() => StatsUtils.SampleVar(null, 0));
			Assert.Throws<ArgumentOutOfRangeException>(() => StatsUtils.SampleVar(new double[] { }, 0));
			Assert.Throws<ArgumentOutOfRangeException>(() => StatsUtils.SampleVar(new double[] { 0 }));
			Assert.Throws<ArgumentOutOfRangeException>(() => StatsUtils.SampleVar(new double[] { 0 }, 0));

			var x = new double[] { 0, 1, -1, 2, -2 };
			IsWithinThreshold(2.5, StatsUtils.SampleVar(x), 1e-5);
			IsWithinThreshold(2.5, StatsUtils.SampleVar(x, 0), 1e-5);

			x = new double[] { 1, 1, 1, 1, 1 };
			Assert.AreEqual(0, StatsUtils.SampleVar(x));
		}

		[Test]
		public void ACorr() {
			var x = new double[] { 2, 3, -1, 5, 3, 2 };
			var acf = new double[] {
				-0.505747,
				-0.011494,
				0.034483,
				-0.022989,
				0.005747,
				0
			};
			Assert.AreEqual(1, StatsUtils.ACorr(0, x));
			for (int i = 1; i <= 6; i++) {
				var acf_hat = StatsUtils.ACorr(i, x);
				IsWithinThreshold(acf[i - 1], acf_hat, 1e-5);
			}
		}

		[Test]
		public void EstimateAROrder() {
			// TODO fix EstimateAROrder
			Random r = new Random();
			for (int p = 1; p < 10; p++) {
				var parameters = new double[p];
				for (int i = 0; i < p; i++) {
					parameters[i] = r.NextDouble();
				}
				var ar = new ARModel(0, parameters);
				// this must be non-zero or else AR(p) = c for all X
				var lastX = 1.0;
				// prime the model with values
				for (int j = 0; j < p; j++) {
					lastX = ar.Predict(lastX);
				}
				var x = new double[100];
				x[0] = lastX;
				for (int j = 1; j < 100; j++) {
					lastX = ar.Predict(lastX);
					x[j] = lastX;
				}

				var p_hat = StatsUtils.EstimateAROrder(x, 10);
				Assert.True(Math.Abs(p - p_hat) < 2, "Incorrect AR order expected {0} but was actually {1}", p, p_hat);
			}
		}

		[Test]
		public void FitAR() {
			var phi = new double[] { 0.5 };
			var X = CreateARSeries(phi, 100);
			var phi_hat = StatsUtils.FitAR(1, X);
			IsWithinThreshold(phi[0], phi_hat[0]);

			//Random r = new Random();
			//for (int p = 2; p < 50; p++) {
			//	for (int i = 0; i < 100; i++) {
			//		var phi = new double[p];
			//		for (int j = 0; j < p; j++) {
			//			phi[j] = r.NextDouble();
			//			if (r.NextDouble() < 0.5)
			//				phi[j] = -phi[j];
			//		}
			//		var X = CreateARSeries(phi, 1000);
			//		var phi_hat = StatsUtils.FitAR((uint)p, X);
			//		Logger.Log("phi = {0}, phi_hat = {1}", string.Join(", ", phi), string.Join(", ", phi_hat));
			//		for (int j = 0; j < p; j++) {
			//			IsWithinThreshold(phi[j], phi_hat[j], 1e-2);
			//		}
			//	}
			//}
		}

		protected double[] CreateARSeries(double[] phi, int n) {
			var ar = new ARModel(0, phi);
			var lastX = 1.0;
			for (int i = 0; i < phi.Length; i++) {
				lastX = ar.Predict(lastX);
			}
			var X = new double[n];
			X[0] = lastX;
			for (int j = 1; j < n; j++) {
				lastX = ar.Predict(lastX);
				X[j] = lastX;
			}
			return X;
		}

		protected const double DEFAULT_THRESHOLD = 1e-5;

		protected void IsWithinThreshold(double expected, double actual) {
			IsWithinThreshold(expected, actual, DEFAULT_THRESHOLD);
		}

		protected void IsWithinThreshold(double expected, double actual, double threshold) {
			Assert.LessOrEqual(actual, expected + threshold);
			Assert.GreaterOrEqual(actual, expected - threshold);
		}
	}
}

