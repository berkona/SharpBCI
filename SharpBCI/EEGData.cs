using System;

namespace SharpBCI {

	/**
	 * What type of EEG data this EEGEvent represents.
	 * IMPORTANT note to devs: not assigning a value to each field in this enum can break scenes that rely on it.
	 */
	public enum EEGDataType {
		/**
		 * This represents microvolt data organized by channel.
		 * It may or may not be filtered for artifacts depending on pipeline layout.
		 * The length of EEGEvent data should equal the number of channels as 
		 * reported by the underlying EEGDeviceAdapter.
		 */
		EEG = 0,

		/**
		 * Data resulting from a Fourier Transform on some number of samples of type = EEG events
		 * This EEGEvent has not been smoothed in any way, and is inherently more variable than FFT_SMOOTHED.
		 * Length of data array depends on window size of the Fourier Transform.
		 * Extra data is an int indicating what channel this event belongs to.
		 */
		FFT_RAW = 1,

		/**
		 * Data resulting from a Fourier Transform on some number of samples of type = EEG events
		 * This EEGEvent has been smoothed by some function which depends on the underlying pipeline.
		 * Length of data array depends on window size of the Fourier Transform.
         * Extra data is an int indicating what channel this event belongs to.		 */
		FFT_SMOOTHED = 2,

		/**
		 * The sum of power in the alpha band (specifically about 7.5 Hz ~ 13 Hz) in decibels
		 * Length of data array is equal to the number of channels as reported by the underlying EEGDeviceAdapter
		 */
		ALPHA_ABSOLUTE = 3,

		/**
		 * The sum of power in the beta band (specifically about 13 Hz ~ 30 Hz) in decibels
		 * Length of data array is equal to the number of channels as reported by the underlying EEGDeviceAdapter
		 */
		BETA_ABSOLUTE = 4,

		/**
		 * The sum of power in the alpha band (specifically about 30 Hz ~ 44 Hz) in decibels
		 * Length of data array is equal to the number of channels as reported by the underlying EEGDeviceAdapter
		 */
		GAMMA_ABSOLUTE = 5,

		/**
		 * The sum of power in the alpha band (specifically about 1 Hz ~ 4 Hz) in decibels
		 * Length of data array is equal to the number of channels as reported by the underlying EEGDeviceAdapter
		 */
		DELTA_ABSOLUTE = 6,

		/**
		* The sum of power in the alpha band (specifically about 4 Hz ~ 8 Hz) in decibels
		* Length of data array is equal to the number of channels as reported by the underlying EEGDeviceAdapter
		*/
		THETA_ABSOLUTE = 7,

		// relative freq bands

		/**
		 * The power of the alpha band in relation to other bands.
         * Length of data array should be equal to the underlying EEGDeviceAdapter.
		 */
		ALPHA_RELATIVE = 8,

		/**
		 * The power of the beta band in relation to other bands.
         * Length of data array should be equal to the underlying EEGDeviceAdapter.		 */
		BETA_RELATIVE = 9,

		/**
		 * The power of the gamma band in relation to other bands.
         * Length of data array should be equal to the underlying EEGDeviceAdapter.		 */
		GAMMA_RELATIVE = 10,

		/**
		 * The power of the delta band in relation to other bands.
         * Length of data array should be equal to the underlying EEGDeviceAdapter.		 */
		DELTA_RELATIVE = 11,

		/**
		 * The power of the theta band in relation to other bands.
         * Length of data array should be equal to the underlying EEGDeviceAdapter.		 */
		THETA_RELATIVE = 12,

		/**
		 * Indicates a change in contact quality for EEG channels
		 * Length of data array should be equal to the underlying EEGDeviceAdapter.
		 * The data array indicates a qualitative metric of current connectivity on a per channel basis.
		 * Current semantics: 4 = no contact, 2 = poor contact, 1 = good contact
		 */
		CONTACT_QUALITY = 13,
	}

	/**
	 * A class which represent various types of events relating to EEG data
	 */
	public class EEGEvent {
		/**
		 * The time at which the given EEGEvent occurred
		 */
		public readonly DateTime timestamp;

		/**
		 * What type of EEGEvent this is
		 * @see EEGDataType
		 */
		public readonly EEGDataType type;

		/**
		 * The data associated with a given EEGEvent.
		 * Semantics depends on type
		 */
		public readonly double[] data;

		/**
		 * An object which stores various extra data that cannot be captured by an array of doubles
		 */
		public readonly object extra;

		/**
		 * Create an EEGEvent without any extra data
		 */
		public EEGEvent(DateTime timestamp, EEGDataType type, double[] data) 
			: this(timestamp, type, data, null) {}

		/**
		 * Create an EEGEvent with additional extra data
		 */
		public EEGEvent(DateTime timestamp, EEGDataType type, double[] data, object extra) {
			this.timestamp = timestamp;
			this.type = type;
			this.data = data;
			this.extra = extra;
		}
        
        /**
         * Converts EEG data to string in predefined format
         */
		public override string ToString () {
			return string.Format ("EEGEvent({0:T}/{1}/{2}/{3})", timestamp, type, string.Join(" ", data), extra);
		}
	}

}
