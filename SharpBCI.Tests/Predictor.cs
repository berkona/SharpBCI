using NUnit.Framework;
using System;


namespace SharpBCI.Tests {

    [TestFixture]
    public class PredictorsTesting
    {

        #region AggregateKNNCorrelationPredictor

        [Test]
		public void AggregateKNNCorrelationPredictor()
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




        #endregion
    }
}
