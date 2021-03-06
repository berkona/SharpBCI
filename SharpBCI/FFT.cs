﻿using System;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;
using DSPLib;

namespace SharpBCI {

	/**
	 * A common interface for a generic smoother which operates on arrays of type T
	 * Currently, only used by FFTPipeable for smoothing
	 */
	public interface IVectorizedSmoother<T> {
		/**
		 * Accept an array of type T and output an array of type T with a smoothing function applied to all values of the input array.
		 */
		T[] Smooth(T[] values);
	}

	/**
	 * Implements a vectorized version of exponential smoothing
	 * Each value in the input array is assumed to belong to the same 
	 * time series as the previous value at the same index in the last input array
	 */
	public class ExponentialVectorizedSmoother : IVectorizedSmoother<double> {
		readonly double[] lastValues;
		readonly double alpha;
		public ExponentialVectorizedSmoother(int n, double alpha) {
			lastValues = new double[n];
			this.alpha = alpha;
		}

		public double[] Smooth(double[] values) {
			if (values.Length != lastValues.Length)
				throw new ArgumentException("values.Length does not match lastValues.Length");
			var n = values.Length;
			for (int i = 0; i < values.Length; i++) {
				lastValues[i] = (alpha * values[i]) + (1 - alpha) * lastValues[i];
			}
			return lastValues;
		}
	}

	public class XCorrVectorizedSmoother : IVectorizedSmoother<Complex> {

		readonly uint iterations;

		readonly Queue<Complex[]> aChannel = new Queue<Complex[]>();
		readonly Queue<Complex[]> bChannel = new Queue<Complex[]>();

		bool turn;

		public XCorrVectorizedSmoother(uint iterations) {
			this.iterations = iterations;
		}

		public Complex[] Smooth(Complex[] values) {
			if (turn) {
				bChannel.Enqueue(values);
				if (bChannel.Count == iterations + 1)
					bChannel.Dequeue();
			} else {
				aChannel.Enqueue(values);
				if (aChannel.Count  == iterations + 1)
					aChannel.Dequeue();
			}
			turn = !turn;

			var N = Math.Min(aChannel.Count, bChannel.Count);
			if (N < 1) return values;

			var aSamples = aChannel.ToArray();
			var bSamples = bChannel.ToArray();

			var accumulator = new Complex[aSamples[0].Length].Select((x) => Complex.Zero);
			for (int i = 0; i < N; i++) {
				var a = aSamples[i];
				var b = bSamples[i];
				var conjugates = b.Select((x) => Complex.Conjugate(x));
				var multiplied = a.Zip(b, (x, y) => x * y);
				accumulator = accumulator.Zip(multiplied, (x, y) => x + y);
			}
			return accumulator.Select((x) => (x / N)).ToArray();
		}
	}

	/**
	 * A Pipeable which performs an FFT on each channel
	 * It outputs an FFTEvent every windowSize samples
	 * 
	 * Input Types: EEGEvent of type RAW
	 * Output Types: EEGEvent of the following types:
	 * - FFT_RAW
	 * - FFT_SMOOTHED
	 * - ALPHA_ABSOLUTE
	 * - BETA_ABSOLUTE
	 * - GAMMA_ABSOLUTE
	 * - DELTA_ABSOLUTE
	 * - THETA_ABSOLUTE
	 * - ALPHA_RELATIVE
	 * - BETA_RELATIVE
	 * - GAMMA_RELATIVE
	 * - THETA_RELATIVE
	 * 
	 * @see FFTEvent
	 */
	public class FFTPipeable : Pipeable {
		
		readonly uint windowSize;
		readonly uint channels;
		readonly uint fftRate;

		readonly Queue<double>[] samples;
		readonly FFT fft;

		readonly double sampleRate;
		readonly double[] windowConstants;
		readonly double scaleFactor;
		//readonly double noiseFactor;

		readonly IVectorizedSmoother<double>[] magSmoothers;
		readonly IVectorizedSmoother<Complex>[] complexFilters;

		//readonly IFilter<double>[] signalFilters;

		uint nSamples = 0;
		uint lastFFT = 0;

		/**
		 * Create a new FFTPipeable which performs an FFT over windowSize.  
		 * targetFFTRate defaults to 10 Hz with this constructor.
		 * @param windowSize The size of the FFT window, determines granularity (google FFT)
		 * @param channels How many channels to operate on
		 * @param sampleRate Sampling rate of data
		 * @see EEGEvent		 */
		public FFTPipeable(int windowSize, int channels, double sampleRate) : this(windowSize, channels, sampleRate, 10) { }

		/**
		 * Create a new FFTPipeable which performs an FFT over windowSize.  Expects an input pipeable of EEGEvent's
		 * @param windowSize The size of the FFT window, determines granularity (google FFT)
		 * @param channels How many channels to operate on
		 * @param sampleRate Sampling rate of data
		 * @param targetFFTRate Optional: Frequency (in Hz) to perform an FFT (exact frequency may vary)
		 * @see EEGEvent
		 */
		public FFTPipeable(int windowSize, int channels, double sampleRate, double targetFFTRate) {
			//this.windowSize = windowSize;

			this.windowSize = (uint) windowSize;
			this.channels = (uint)channels;
			this.sampleRate = sampleRate;

			fft = new FFT();
			fft.Initialize((uint) windowSize);

			// target 10Hz
			fftRate = (uint)Math.Round(sampleRate / targetFFTRate);

			samples = new Queue<double>[channels];

			//signalFilters = new IFilter<double>[channels];
			magSmoothers = new IVectorizedSmoother<double>[channels];
			complexFilters = new IVectorizedSmoother<Complex>[channels];
			for (int i = 0; i < channels; i++) {
				samples[i] = new Queue<double>();
				magSmoothers[i] = new ExponentialVectorizedSmoother(windowSize / 2 + 1, 1.0 / (fftRate / 4.0));
				complexFilters[i] = new XCorrVectorizedSmoother(fftRate / 2);
				//signalFilters[i] = new MultiFilter<double>(new IFilter<double>[] {
				//	new ConvolvingDoubleEndedFilter(1, 50, 2, sampleRate, true),
				//	//new MovingAverageFilter(fftRate),
				//});
			}

			windowConstants = DSP.Window.Coefficients(DSP.Window.Type.Hamming, this.windowSize);
			scaleFactor = DSP.Window.ScaleFactor.Signal(windowConstants);
			//noiseFactor = DSP.Window.ScaleFactor.Noise(windowConstants, sampleRate);
		}

		protected override bool Process(object item) {
			EEGEvent evt = (EEGEvent) item;
			if (evt.type != EEGDataType.EEG)
				throw new Exception("FFTPipeable recieved invalid EEGEvent: " + evt);

			if (evt.data.Length != channels)
				throw new Exception("FFTPipeable recieved malformed EEGEvent: " + evt);

			// normal case: just append data to sample buffer
			for (int i = 0; i < channels; i++) {
				var v = evt.data[i];
				//v = signalFilters[i].Filter(v);
				samples[i].Enqueue(v);
			}
			nSamples++;

			if (nSamples > windowSize) {
				foreach (var channelSamples in samples) {
					channelSamples.Dequeue();
					//channelSamples.Dequeue();
				}
				nSamples--;
			}

			lastFFT++;
			//Logger.Log("nSamples={0}, lastFFT={1}", nSamples, lastFFT);
			// sample buffer is full, do FFT then reset for next round
			if (nSamples >= windowSize && lastFFT % fftRate == 0) {
				DoFFT(evt);
			}

			return true;
		}

		void DoFFT(EEGEvent evt) { 
			// Do an FFT on each channel
			List<double[]> fftOutput = new List<double[]>();
			for (int i = 0; i < samples.Length; i++) {
				var channelSamples = samples[i];
				var samplesCopy = channelSamples.ToArray();

				// apply windowing function to samplesCopy
				DSP.Math.Multiply(samplesCopy, windowConstants);

				var cSpectrum = fft.Execute(samplesCopy);
				// complex side smoothing
				//cSpectrum = complexFilters[i].Smooth(cSpectrum);

				double[] lmSpectrum = DSP.ConvertComplex.ToMagnitude(cSpectrum);
				lmSpectrum = DSP.Math.Multiply(lmSpectrum, scaleFactor);

				fftOutput.Add(lmSpectrum);
			}

			for (int i = 0; i < fftOutput.Count; i++) {
				var rawFFT = fftOutput[i];
				Add(new EEGEvent(evt.timestamp, EEGDataType.FFT_RAW, rawFFT, i));

				// magnitude side smoothing
				var smoothedFFT = magSmoothers[i].Smooth(rawFFT);
				Add(new EEGEvent(evt.timestamp, EEGDataType.FFT_SMOOTHED, smoothedFFT, i));
			}

			//var freqSpan = fft.FrequencySpan(sampleRate);

			// find abs powers for each band
			var absolutePowers = new Dictionary<EEGDataType, double[]>();
			for (int i = 0; i < channels; i++) {
				var bins = fftOutput[i];
				double deltaAbs = AbsBandPower(bins, 1, 4);
				double thetaAbs = AbsBandPower(bins, 4, 8);
				double alphaAbs = AbsBandPower(bins, 7.5, 13);
				double betaAbs = AbsBandPower(bins, 13, 30);
				double gammaAbs = AbsBandPower(bins, 30, 44);
				//Logger.Log("D={0}, T={1}, A={2}, B={3}, G={4}", deltaAbs, thetaAbs, alphaAbs, betaAbs, gammaAbs);

				GetBandList(absolutePowers, EEGDataType.ALPHA_ABSOLUTE)[i] = (alphaAbs);
				GetBandList(absolutePowers, EEGDataType.BETA_ABSOLUTE)[i] = (betaAbs);
				GetBandList(absolutePowers, EEGDataType.GAMMA_ABSOLUTE)[i] = (gammaAbs);
				GetBandList(absolutePowers, EEGDataType.DELTA_ABSOLUTE)[i] = (deltaAbs);
				GetBandList(absolutePowers, EEGDataType.THETA_ABSOLUTE)[i] = (thetaAbs);
			}

			// we can emit abs powers immediately
			Add(new EEGEvent(evt.timestamp, EEGDataType.ALPHA_ABSOLUTE, absolutePowers[EEGDataType.ALPHA_ABSOLUTE].ToArray()));
			Add(new EEGEvent(evt.timestamp, EEGDataType.BETA_ABSOLUTE, absolutePowers[EEGDataType.BETA_ABSOLUTE].ToArray()));
			Add(new EEGEvent(evt.timestamp, EEGDataType.GAMMA_ABSOLUTE, absolutePowers[EEGDataType.GAMMA_ABSOLUTE].ToArray()));
			Add(new EEGEvent(evt.timestamp, EEGDataType.DELTA_ABSOLUTE, absolutePowers[EEGDataType.DELTA_ABSOLUTE].ToArray()));
			Add(new EEGEvent(evt.timestamp, EEGDataType.THETA_ABSOLUTE, absolutePowers[EEGDataType.THETA_ABSOLUTE].ToArray()));

			// now calc and emit relative powers
			Add(new EEGEvent(evt.timestamp, EEGDataType.ALPHA_RELATIVE, RelBandPower(absolutePowers, EEGDataType.ALPHA_ABSOLUTE)));
			Add(new EEGEvent(evt.timestamp, EEGDataType.BETA_RELATIVE, RelBandPower(absolutePowers, EEGDataType.BETA_ABSOLUTE)));
			Add(new EEGEvent(evt.timestamp, EEGDataType.GAMMA_RELATIVE, RelBandPower(absolutePowers, EEGDataType.GAMMA_ABSOLUTE)));
			Add(new EEGEvent(evt.timestamp, EEGDataType.DELTA_RELATIVE, RelBandPower(absolutePowers, EEGDataType.DELTA_ABSOLUTE)));
			Add(new EEGEvent(evt.timestamp, EEGDataType.THETA_RELATIVE, RelBandPower(absolutePowers, EEGDataType.THETA_ABSOLUTE)));
		}

		double[] GetBandList(Dictionary<EEGDataType, double[]> dict, EEGDataType type) {
			if (dict.ContainsKey(type)) {
				return dict[type];
			} else {
				double[] val = new double[channels];
				dict[type] = val;
				return val;
			}
		}

		double[] RelBandPower(Dictionary<EEGDataType, double[]> powerDict, EEGDataType band) {
			double[] absPowers = new double[channels];
			foreach (var channelPowers in powerDict) {
				//Logger.Log("Looking at " + channelPowers.Key);
				for (int i = 0; i < channels; i++) {
					absPowers[i] += Math.Pow(channelPowers.Value[i], 10);
				}
			}

			double[] relPowers = new double[channels];
			double[] bandPowers = powerDict[band];
			for (int i = 0; i < channels; i++) {
				relPowers[i] =  Math.Pow(bandPowers[i], 10) / absPowers[i];
			}
			return relPowers;
		}

		double AbsBandPower(double[] bins, double minFreq, double maxFreq) {
			double halfSampleRate = sampleRate / 2;
			double points = (double)(windowSize / 2);
			int minBin = (int)Math.Floor(minFreq / (halfSampleRate / points));
			int maxBin = (int)Math.Ceiling(maxFreq / (halfSampleRate / points));
			double powerSum = 0;
			for (int i = minBin; i <= maxBin; i++) {
				powerSum += bins[i];
			}
			return Math.Log10(powerSum);
		}
	}
}
