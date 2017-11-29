using NUnit.Framework;
using System;


namespace SharpBCI.Tests {

    [TestFixture]
    public class PredictorsTesting
    {

        #region AggregateKNNCorrelationPredictor

        [Test]
        public void ARModelConstructor()
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

        #region DBNPredictor
        [Test]
        public void DeepBeliefNetwork()
        {
            DeepBeliefNetworkPredictor predictor = new DeepBeliefNetworkPredictor(1, new EEGDataType[] { EEGDataType.ALPHA_RELATIVE, EEGDataType.GAMMA_RELATIVE });

            Random r = new Random(1131);
            for (int attempts = 0; attempts < 10; attempts++)
            {
                var x = new double[12];
                for (int i = 0; i < 12; i++)
                {
                    x[i] = r.NextDouble();
                }

                predictor.AddTrainingData(1, x);
            }

            Assert.IsEmpty(predictor.ToString());



        }
        #endregion
    }
}
