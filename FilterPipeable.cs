using System;
using System.Collections.Generic;

namespace SharpBCI {
	
	public class SimpleFilterPipeable : Pipeable {

		// signal filters fields
		readonly IFilter<double>[] signalFilters;

		// artifact fields
		readonly int artifactLearningSize;
		readonly Queue<double>[] artifactLearningSamples;
		readonly double[] lastPredictions;
		readonly ARModel[] arPredictors;
		readonly OnlineVariance[] arError;

		bool isLearning = true;
		// end artifact fields

		public SimpleFilterPipeable(
			double sampleRate,
			int channels,
			double minFreq,
			double maxFreq,
			double transitionBandwidth,
			double artifactLearningTime) {

			artifactLearningSize = (int) Math.Round(sampleRate * artifactLearningTime);

			signalFilters = new IFilter<double>[channels];
			artifactLearningSamples = new Queue<double>[channels];
			arPredictors = new ARModel[channels];
			lastPredictions = new double[channels];
			arError = new OnlineVariance[channels];

			for (int i = 0; i < channels; i++) {
				signalFilters[i] = new ConvolvingDoubleEndedFilter(minFreq, maxFreq, transitionBandwidth, sampleRate, true);
				artifactLearningSamples[i] = new Queue<double>();
				arError[i] = new OnlineVariance();
			}
		}

		protected override bool Process(object item) {
			EEGEvent evt = (EEGEvent)item;

			var n = evt.data.Length;
			double[] buffer = new double[evt.data.Length];
			for (int i = 0; i < n; i++) {
				buffer[i] = signalFilters[i].Filter(evt.data[i]);
			}

			// TODO test artifact detection
			if (isLearning) {
				// add samples to window and test if window is sufficiently large
				bool stillLearning = false;
				for (int i = 0; i < n; i++) {
					artifactLearningSamples[i].Enqueue(evt.data[i]);
					stillLearning = artifactLearningSamples[i].Count < artifactLearningSize;
				}
				// sufficient samples to fit AR model
				if (!stillLearning) {
					for (int i = 0; i < n; i++) {
						double[] arParams = StatsUtils.FitAR(10, artifactLearningSamples[i].ToArray());
						arPredictors[i] = new ARModel(0, arParams);
						lastPredictions[i] = arPredictors[i].Predict(evt.data[i]);
					}
				}
				isLearning = stillLearning;
			} else {
				bool isArtifact = false;
				for (int i = 0; i < n; i++) {
					var x = evt.data[i];
					var error = x - lastPredictions[i];
					var errorDist = arError[i];
					errorDist.Update(error);
					lastPredictions[i] = arPredictors[i].Predict(x);

					if (errorDist.isValid) {
						var errorS = Math.Sqrt(errorDist.var);
						var errorMaxCI95 = errorDist.mean + 2 * errorS;
						var errorMinCI95 = errorDist.mean + -2 * errorS;
						isArtifact = error > errorMaxCI95 || error < errorMinCI95;
					}
				}

				if (isArtifact)
					return true;
			}

			Add(new EEGEvent(evt.timestamp, evt.type, buffer, evt.extra));

			return true;
		}
	}
}