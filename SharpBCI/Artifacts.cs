using System;
using System.Linq;
using System.Collections.Generic;

namespace SharpBCI {

	/**
	 * An interface which defines an abstract artifact detector
	 * You can implement this to utilize classes which find the best detector implementation
	 * @see TournamentArtifactDetector
	 */
	public interface IArtifactDetector {
		/**
		 * Update artifact detector using next
		 * @returns true iff next is an artifact, false otherwise
		 */
		bool Detect(double next);

		/**
		 * Inform the caller of the current confusion of this implementation
		 * The returned value should be a double greater than or equal to zero
		 * Otherwise no range is assumed, but lower values should indicate lower confusion
		 * For example, a detector that perfectly models the signal with no artifacts should have an error of zero
		 * Pragmatically, error will generally be non-zero due to presence of artifacts and noise
		 * @returns the internal confusion metric of this implementation
		 */
		double Error();
	}

	/**
	 * A detector which uses an underlying AR model to detect artifacts
	 */
	public class ARArtifactDetector : IArtifactDetector {
		public const double ARTIFACT_THRESHOLD = 2;

		readonly ARModel model;
		readonly OnlineVariance errorDist;
		readonly OnlineVariance confusionDist;

		double lastPrediction;

		public ARArtifactDetector(ARModel model) {
			if (model == null)
				throw new ArgumentException();
			
			this.model = model;
			errorDist = new OnlineVariance();
			confusionDist = new OnlineVariance();
		}

		public double Error() {
			return confusionDist.var;
		}

		public bool Detect(double next) {
			var nextPrediction = model.Predict(next);
			var error = next - lastPrediction;
			lastPrediction = nextPrediction;

			confusionDist.Update(error);

			//Logger.Log("Error={0}", error);
			if (errorDist.isValid) {
				var errorS = Math.Sqrt(errorDist.var);
				var errorMaxCI95 = errorDist.mean + ARTIFACT_THRESHOLD * errorS;
				var errorMinCI95 = errorDist.mean - ARTIFACT_THRESHOLD * errorS;
				//Logger.Log("Error={0}, 95% CI = [{1}, {2}]", error, errorMinCI95, errorMaxCI95);
				var isArtifact = error > errorMaxCI95 || error < errorMinCI95;
				if (!isArtifact) errorDist.Update(error);
				return isArtifact;
			} else {
				errorDist.Update(error);
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

		public TournamentArtifactDectector(uint tournamentSize, uint learningSetSize, uint nAccept, uint initialMerits) {
			// arg checking
			if (tournamentSize == 0 
			    || learningSetSize == 0 
			    || nAccept == 0
			    || initialMerits == 0
			    || nAccept > tournamentSize) {
				throw new ArgumentException();
			}

			this.nAccept = nAccept;
			this.initialMerits = (int) initialMerits;
			this.learningSetSize = learningSetSize;

			competitors = new IArtifactDetector[tournamentSize];
			demerits = new int[tournamentSize];
			latestSamples = new Queue<double>();
		}

		public double Error() {
			return competitors.Sum((arg) => arg.Error()) / competitors.Length;
		}

		public bool Detect(double data) {
			// handle learning set samples
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

			// normal operation logic

			// ask all competitors to detect if the next data point is an artifact
			// then order them by ascending error 
			// the consensus is the plurality of the highest-ranked nAccept detectors
			var consensus = competitors
				.Zip(competitors.Select(x => x.Detect(data)), Tuple.Create)
				.OrderBy(x => x.Item1.Error())
				.Take((int)nAccept)
				.GroupBy(x => x.Item2)
				.OrderByDescending(g => g.Count())
				.First().Key;

			var n = competitors.Length;
			var midpoint = n / 2;
			for (int i = 0; i < n; i++) {
				var pointsToAward = i >= midpoint ? -1 : 1;
				demerits[i] = Math.Max(initialMerits, demerits[i] + pointsToAward);
				if (demerits[i] < 0) {
					demerits[i] = initialMerits;
					competitors[i] = NewCompetitor();
				}
			}

			return consensus;
		}

		protected IArtifactDetector NewCompetitor() {
			if (latestSamples.Count < learningSetSize) {
				throw new InvalidOperationException();
			}

			double[] x = latestSamples.ToArray();
			var p = StatsUtils.EstimateAROrder(x, 50);
			double mean = StatsUtils.SampleMean(x);
			double[] arParams = StatsUtils.FitAR(p, x);
			Logger.Log("Created new competitor: p={0}, c={1}, phi={2}", p, mean, string.Join(", ", arParams));
			return new ARArtifactDetector(new ARModel(mean, arParams));
		}
	}

}
