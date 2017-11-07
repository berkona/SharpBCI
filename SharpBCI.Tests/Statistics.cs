using NUnit.Framework;
using System;

namespace SharpBCI.Tests {

	[TestFixture]
	public class StatisticsTesting {

		#region ARModel

		[Test]
		public void ARModelConstructor() {
			Assert.Throws<ArgumentOutOfRangeException>(() => new ARModel(double.NaN, null));
			Assert.Throws<ArgumentOutOfRangeException>(() => new ARModel(double.NegativeInfinity, null));
			Assert.Throws<ArgumentOutOfRangeException>(() => new ARModel(double.PositiveInfinity, null));
			Assert.Throws<ArgumentOutOfRangeException>(() => new ARModel(0, null));
			Assert.Throws<ArgumentOutOfRangeException>(() => new ARModel(0, new double[] { }));
			Assert.DoesNotThrow(() => new ARModel(0, new double[] { 0.5 }));
		}

		[Test]
		public void ARModelPredict() {
			var ar1 = new ARModel(0, new double[] { 0.5 });
			var X = new double[] {
				1,
				1, 
				1.0 * 0.5, 
				1.0 * 0.5 * 0.5, 
				1.0 * 0.5 * 0.5 * 0.5, 
				1.0 * 0.5 * 0.5 * 0.5 * 0.5,
			};

			for (int i = 0; i < X.Length-1; i++) {
				Assert.AreEqual(X[i + 1], ar1.Predict(X[i]));
			}

			var ar2 = new ARModel(0, new double[] { 0.3, 0.3 });
			X = new double[] {
				1,
				1,
				1,
				0.6,
				0.48,
				0.324,
				0.2412,
				0.16956,
			};

			for (int i = 0; i < X.Length - 1; i++) {
				IsWithinThreshold(X[i + 1], ar2.Predict(X[i]));
			}
		}

		#endregion

		#region StatsUtils

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
		public void ACF() {
			var x = new double[] { 2, 3, -1, 5, 3, 2 };
			var acf = new double[] {
				-0.505747,
				-0.011494,
				0.034483,
				-0.022989,
				0.005747,
				0
			};
			Assert.AreEqual(1, StatsUtils.ACF(0, x));
			for (int i = 1; i <= 6; i++) {
				var acf_hat = StatsUtils.ACF(i, x);
				IsWithinThreshold(acf[i - 1], acf_hat, 1e-5);
			}
		}

		[Test]
		public void PACF() {
			var X = MakeSeries(new double[] { 0.5 }, 10000, 0.25);
			IsWithinThreshold(0.5, StatsUtils.PACF(1, X), 0.05);
			IsWithinThreshold(0, StatsUtils.PACF(2, X), 0.05);
            IsWithinThreshold(0, StatsUtils.PACF(3, X), 0.05);
            IsWithinThreshold(0, StatsUtils.PACF(4, X), 0.05);

			// This doesn't work atm
			//X = MakeSeries(new double[] { 0.3, 0.3 }, 10000, 0.25);
			//IsWithinThreshold(0.3, StatsUtils.PACF(1, X), 0.05);
			//IsWithinThreshold(0.3, StatsUtils.PACF(2, X), 0.05);
			//IsWithinThreshold(0, StatsUtils.PACF(3, X), 0.05);
			//IsWithinThreshold(0, StatsUtils.PACF(4, X), 0.05);
            //IsWithinThreshold(0, StatsUtils.PACF(5, X), 0.05);
		}

		Random noiseRandom;

		[SetUp]
		protected void CreateNoise() {
			noiseRandom = new Random();
		}

		protected double Noise() {
			return noiseRandom.NextDouble() - 0.5;
		}

		protected double[] MakeSeries(double[] phi, int n, double noiseFactor) {
			var p = phi.Length;
			var X = new double[n];
			for (int i = 0; i < p; i++) {
				X[i] = 1 + noiseFactor * Noise();
			}
			for (int i = p; i < n; i++) {
				double xi = noiseFactor * Noise();
				for (int j = 0; j < p; j++) {
					xi += X[i - j - 1] * phi[j];
				}
				X[i] = xi;
			}
			return X;
		}

		[Test]
		public void FitAR() {
			// AR(1)
			var X = MakeSeries(new double[] { 0.5 }, 10000, 0.25);
			//Logger.Log("X = {0}", StatsUtils.Summary(X));
			IsWithinThreshold(0.5, StatsUtils.FitAR(1, X)[0], 0.05);

			// AR(2)
			X = MakeSeries(new double[] { 0.3, 0.3 }, 10000, 0.25);
			//Logger.Log("X = {0}", StatsUtils.Summary(X));
			var phi_hat = StatsUtils.FitAR(2, X);
			IsWithinThreshold(0.3, phi_hat[0], 0.05);
			IsWithinThreshold(0.3, phi_hat[1], 0.05);

			// AR(3)
			X = MakeSeries(new double[] { 0.3, 0.3, 0.3 }, 10000, 0.25);
			//Logger.Log("X = {0}", StatsUtils.Summary(X));
			phi_hat = StatsUtils.FitAR(3, X);
			IsWithinThreshold(0.3, phi_hat[0], 0.05);
			IsWithinThreshold(0.3, phi_hat[1], 0.05);
            IsWithinThreshold(0.3, phi_hat[2], 0.05);
		}

		#endregion

		#region OnlineVariance

		[Test]
		public void OnlineVariance() {
			var v = new OnlineVariance();
			Assert.IsFalse(v.isValid);
			Assert.AreEqual(0, v.mean);
			Assert.IsNaN(v.var);
			v.Update(1);
			Assert.IsFalse(v.isValid);
			Assert.AreEqual(1, v.mean);
			Assert.IsNaN(v.var);
			v.Update(1);
			Assert.IsTrue(v.isValid);
			Assert.AreEqual(1, v.mean);
			Assert.AreEqual(0, v.var);
		}

		#endregion

		protected const double DEFAULT_THRESHOLD = 1e-5;

		bool loggerSetup = false;

		[SetUp]
		protected void SetupLogger() {
			if (!loggerSetup) {
				Logger.AddLogOutput(new ConsoleLogger());
				loggerSetup = true;
			}
		}

		protected void IsWithinThreshold(double expected, double actual) {
			IsWithinThreshold(expected, actual, DEFAULT_THRESHOLD);
		}

		protected void IsWithinThreshold(double expected, double actual, double threshold) {
			Assert.LessOrEqual(actual, expected + threshold);
			Assert.GreaterOrEqual(actual, expected - threshold);
		}
	}
}

