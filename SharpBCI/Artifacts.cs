using System;
using System.Linq;
using System.Collections.Generic;

namespace SharpBCI {

	public interface IArtifactDetector {
		/**
		 * Update artifact detector using next
		 * @returns true iff next is an artifact, false otherwise
		 */
		bool Detect(double next);	
	}

	/**
	 * A detector which uses an underlying AR model to detect artifacts
	 */
	public class ARArtifactDetector : IArtifactDetector {
		public const double ARTIFACT_THRESHOLD = 2;

		readonly ARModel model;
		readonly OnlineVariance errorDist;
		double lastPrediction;

		public ARArtifactDetector(ARModel model) {
			this.model = model;
			errorDist = new OnlineVariance();
		}

		public bool Detect(double next) {
			var nextPrediction = model.Predict(next);
			var error = next - lastPrediction;
			lastPrediction = nextPrediction;

			errorDist.Update(error);
			if (errorDist.isValid) {
				var errorS = Math.Sqrt(errorDist.var);
				var errorMaxCI95 = errorDist.mean + ARTIFACT_THRESHOLD * errorS;
				var errorMinCI95 = errorDist.mean - ARTIFACT_THRESHOLD * errorS;
				//Logger.Log("Error={0}, 95% CI = [{1}, {2}]", error, errorMinCI95, errorMaxCI95);
				var isArtifact = error > errorMaxCI95 || error < errorMinCI95;
				return isArtifact;
			} else {
				return false;
			}
		}
	}

	public class TournamentArtifactDectector : IArtifactDetector {

		readonly uint learningSetSize;
		readonly uint nAccept;
		readonly int initialMerits;

		readonly int[] demerits;
		readonly Queue<double> latestSamples;
		readonly IArtifactDetector[] competitors;

		int nInitted = 0;
		int lastInitted = 0;

		public TournamentArtifactDectector(uint tournamentSize, uint learningSetSize, uint nAccept, int initialMerits) {
			// arg checking
			if (tournamentSize == 0 
			    || learningSetSize == 0 
			    || nAccept == 0) {
				throw new ArgumentOutOfRangeException();
			}

			this.nAccept = nAccept;
			this.initialMerits = initialMerits;
			this.learningSetSize = learningSetSize;

			competitors = new IArtifactDetector[tournamentSize];
			demerits = new int[tournamentSize];
			latestSamples = new Queue<double>();

			// init with a bunch of dummies
			for (int i = 0; i < tournamentSize; i++) {
				// initially, all competitors think that the next signal will be the same as the last
				competitors[i] = NewCompetitor();
				demerits[i] = initialMerits;
			}
		}

		public bool Detect(double data) {
			// handle keeping samples
			// TODO examine difference between keeping potential artifact signals
			latestSamples.Enqueue(data);
			if (latestSamples.Count == learningSetSize + 1) {
				latestSamples.Dequeue();
			}

			// initialization logic
			if (nInitted < competitors.Length) {
				lastInitted++;

				// initialize all models to non-overlapping sets of data
				if (lastInitted == learningSetSize) {
					// assume latestSamples.Count == learningSetSize
					competitors[nInitted] = NewCompetitor();
					demerits[nInitted] = initialMerits;
					nInitted++;
					lastInitted = 0;
				}

				return false;
			}

			// assume latestSamples.Count == learningSetSize+1
			latestSamples.Dequeue();

			var n = competitors.Length;
			var predictions = new bool[n];
			for (int i = 0; i < n; i++) {
				predictions[i] = competitors[i].Detect(data);
			}

			// sort all competitors
			var rankedPredictions = predictions
				.Zip(demerits, Tuple.Create)
				.OrderByDescending(x => x.Item2)
				.Select(x => x.Item1).ToList();
			
			var consensus = rankedPredictions
				.Take((int) nAccept)
				.GroupBy(x => x)
				.OrderByDescending(g => g.Count())
				.First().Key;

			// award merits/demerits
			for (int i = 0; i < n; i++) {
				demerits[i] = Math.Max(initialMerits, demerits[i] + (predictions[i] == consensus ? 1 : -1));
				if (demerits[i] < 0) {
					demerits[i] = initialMerits;
					competitors[i] = NewCompetitor();
				}
			}

			return consensus;
		}

		protected IArtifactDetector NewCompetitor() {
			if (latestSamples.Count < learningSetSize) {
				return new ARArtifactDetector(new ARModel(0, new double[] { 1 }));
			} else {
				double[] x = latestSamples.ToArray();
				var p = StatsUtils.EstimateAROrder(x, 50);
				double mean = StatsUtils.SampleMean(x);
				double[] arParams = StatsUtils.FitAR(p, x);
                Logger.Log("Created new competitor: p={0}, c={1}, phi={2}", p, mean, string.Join(", ", arParams));
				return new ARArtifactDetector(new ARModel(mean, arParams));
			}
		}
	}

}
