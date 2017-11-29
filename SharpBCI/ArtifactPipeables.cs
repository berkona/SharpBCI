using System;
using System.Linq;

namespace SharpBCI {

	/**
	 * An artifact detector which uses a TournamentArtifactDectector on a per-channel basis to detect artifacts
	 * Input types: EEG, Output types: EEG
	 */
	public class TournamentArtifactPipeable : Pipeable {

		readonly IArtifactDetector[] detectors;

		/**
		 * Create a new TournamentArtifactPipeable with following params
		 * 
		 * @param channels channels as reported by the underlying EEGDeviceAdapter
		 * @param sampleRate sample rate as reported by the underlying EEGDeviceAdapter
		 * @param learningTime This many seconds of data will be used to fit the underlying TournamentArtifactPipeable
		 * @param tournamentSize how many IArtifactDetectors should compete
		 * @param nAccept the majority of the top nAccept IArtifactDetector's determines if the EEGEvent is considered to be an artifact
		 * @param initialMerits determines how quickly an IArtifactDetector is thrown out of the tournament in the worst case.  Should be large in relation to sampleRate.
		 * 
		 */
		public TournamentArtifactPipeable(int channels, double sampleRate, double learningTime, uint tournamentSize, uint nAccept, uint initialMerits) {
			uint learningSampleSize = (uint) Math.Round(learningTime * sampleRate);
			detectors = new IArtifactDetector[channels];
			for (int i = 0; i < channels; i++) {
				detectors[i] = new TournamentArtifactDetector(tournamentSize, learningSampleSize, nAccept, initialMerits);
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
