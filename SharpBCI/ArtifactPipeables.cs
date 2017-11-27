using System;
using System.Linq;

namespace SharpBCI {
	
	public class TournamentArtifactPipeable : Pipeable {

		IArtifactDetector[] detectors;

		public TournamentArtifactPipeable(int channels, double sampleRate, double learningTime, uint tournamentSize, uint nAccept, int initialMerits) {
			uint learningSampleSize = (uint) Math.Round(learningTime * sampleRate);
			detectors = new IArtifactDetector[channels];
			for (int i = 0; i < channels; i++) {
				detectors[i] = new TournamentArtifactDectector(tournamentSize, learningSampleSize, nAccept, initialMerits);
			}
		}

		protected override bool Process(object item) {
			EEGEvent evt = (EEGEvent) item;
			for (int i = 0; i < detectors.Length; i++) {
				if (detectors[i].Detect(evt.data[i]))
					return true;
			}

			Add(evt);
			return true;
		}

	}

}
