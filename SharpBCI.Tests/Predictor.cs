using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace SharpBCI.Tests {

    [TestFixture]
    public class PredictorsTesting
    {

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
