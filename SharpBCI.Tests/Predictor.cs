using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading;

namespace SharpBCI.Tests {

	[TestFixture]
	public class PredictorsTesting {

		#region PredictorRegressionTesting

		public class RaceCondTestPredictor : Predictor {
			public volatile bool canceled = false;
			public volatile bool isPredicting = false;

			public RaceCondTestPredictor(int channels, int k, double thresholdProb, EEGDataType[] bands)
				: base(channels, k, thresholdProb, bands) { }

			protected override double Compute(double[] x, double[] y) {
				isPredicting = true;
				while (!canceled) {}
				isPredicting = false;
				return 0;
			}
		}

		[Test]
		public void CTDRaceCondition() {
			var ctdPredictor = new RaceCondTestPredictor(2, 3, 0.1, new EEGDataType[] { EEGDataType.ALPHA_ABSOLUTE });
			var trainingData = new EEGEvent[][] {
					new EEGEvent[] { new EEGEvent(DateTime.UtcNow, EEGDataType.ALPHA_ABSOLUTE, new double[] { 1, 1 })
				}
			};
			for (int i = 0; i < 5; i++) {
				ctdPredictor.AddTrainingData(1, trainingData[0]);
			}

			Exception lastException = null;

			ThreadStart t1Method = delegate {
				try {
					var p = ctdPredictor.Predict(trainingData[0]);
					Assert.AreEqual(Predictor.NO_PREDICTION, p, "Should return a null prediction on case of clear during prediction");
				} catch (Exception e) {
					lastException = e;
				}
			};

			ThreadStart t2Method = delegate {
				try {
					while (!ctdPredictor.isPredicting) { }
					ctdPredictor.ClearTrainingData();
					ctdPredictor.canceled = true;
				} catch (Exception e) {
					lastException = e;
				}
			};

			var t1 = new Thread(t1Method);
			var t2 = new Thread(t2Method);

			t1.Start();
			t2.Start();

			t1.Join();
			t2.Join();

			if (lastException != null)
				throw lastException;
		}

		#endregion

		#region AggregateKNNCorrelationPredictor

		[Test]
		public void Constructor()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new AggregateKNNCorrelationPredictor(0, 2, .1, new EEGDataType[] { EEGDataType.ALPHA_ABSOLUTE }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AggregateKNNCorrelationPredictor(-2, 2, .1, new EEGDataType[] { EEGDataType.ALPHA_ABSOLUTE }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AggregateKNNCorrelationPredictor(2, 0, .1, new EEGDataType[] { EEGDataType.ALPHA_ABSOLUTE }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AggregateKNNCorrelationPredictor(2, -3, .1, new EEGDataType[] { EEGDataType.ALPHA_ABSOLUTE }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AggregateKNNCorrelationPredictor(2, 0, .1, new EEGDataType[] { EEGDataType.ALPHA_ABSOLUTE }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AggregateKNNCorrelationPredictor(2, 3, -.1, new EEGDataType[] { EEGDataType.ALPHA_ABSOLUTE }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AggregateKNNCorrelationPredictor(2, 3, 1, new EEGDataType[] { EEGDataType.ALPHA_ABSOLUTE }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AggregateKNNCorrelationPredictor(2, 3, 1.1, new EEGDataType[] { EEGDataType.ALPHA_ABSOLUTE }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AggregateKNNCorrelationPredictor(2, 3, 1.1, new EEGDataType[] { }));
            Assert.DoesNotThrow(() => new AggregateKNNCorrelationPredictor(2, 3, .3, new EEGDataType[] { EEGDataType.ALPHA_ABSOLUTE }));
        }

		[Test]
		public void AddTrainingData() {
			var predictor = new AggregateKNNCorrelationPredictor(2, 3, 0.1, new EEGDataType[] { EEGDataType.ALPHA_ABSOLUTE });
			var trainingData = new EEGEvent[][] {
					new EEGEvent[] { new EEGEvent(DateTime.UtcNow, EEGDataType.ALPHA_ABSOLUTE, new double[] { 1, 1 })
				},
			};
			predictor.AddTrainingData(1, trainingData[0]);
		}

		[Test]
		public void Predict() {
			var predictor = new AggregateKNNCorrelationPredictor(2, 10, 0.9, new EEGDataType[] { EEGDataType.ALPHA_ABSOLUTE });

			double[] values = { 0, 100, -100 };
			int dataLength = 100;
			var trainingData = new List<EEGEvent[]>[values.Length];
			for (int i = 0; i < values.Length; i++) {
				trainingData[i] = new List<EEGEvent[]>();
				for (int j = 0; j < dataLength; j++) {
					trainingData[i].Add(new EEGEvent[] { new EEGEvent(DateTime.UtcNow, EEGDataType.ALPHA_ABSOLUTE, new double[] { values[i], values[i] }) });
				}
			}

			for (int i = 0; i < trainingData.Length; i++) {
				foreach (var pt in trainingData[i]) {
					predictor.AddTrainingData(i, pt);
				}
			}

			for (int i = 0; i < trainingData.Length; i++) {
				foreach (var pt in trainingData[i]) {
					predictor.Predict(pt);
					// TODO 
					//Assert.AreEqual(i, predictor.Predict(pt), "Did not predict correct value");
				}
			}
		}


        #endregion
    }
}
