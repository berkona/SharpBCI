﻿using System;
using System.Linq;
using System.Threading;
using System.Collections.Generic;
using SharpOSC;
using DSPLib;

namespace SharpBCI {
	
	public abstract class EEGDeviceAdapter {

		public delegate void DataHandler(EEGEvent evt);

		readonly Dictionary<EEGDataType, List<DataHandler>> handlers = new Dictionary<EEGDataType, List<DataHandler>>();

		readonly Queue<EEGEvent> eventQueue = new Queue<EEGEvent>();

		public readonly int channels;
		public readonly double sampleRate;

		protected EEGDeviceAdapter(int channels, double sampleRate) {
			this.channels = channels;
			this.sampleRate = sampleRate;
		}

		public abstract void Start();
		public abstract void Stop();

		public void AddHandler(EEGDataType type, DataHandler handler) {
			Logger.Log("AddHandler type="+type);
			if (!handlers.ContainsKey(type)) {
				handlers.Add(type, new List<DataHandler>());
			}
			handlers[type].Add(handler);
		}

		public void RemoveHandler(EEGDataType type, DataHandler handler) {
			// Debug.Log("RemoveHandler type="+type);
			if (!handlers.ContainsKey(type))
				throw new Exception("Handler was not registered");

			if (!handlers[type].Remove(handler))
				throw new Exception("Handler was not registered");
		}

		public void FlushEvents() {
			//Logger.Log("FlushEvents()");
			lock (eventQueue) {
				//Logger.Log("FlushEvents lock obtained");
				while (eventQueue.Count > 0) {
					EEGEvent evt = eventQueue.Dequeue();
					FlushEvent(evt);
				}
				//Logger.Log("FlushEvents lock released");
			}
		}

		protected void EmitData(EEGEvent evt) {
			//Logger.Log("EmitData type=" + evt.type);
			lock (eventQueue) {
				//Logger.Log("EmitData lock obtained");
				eventQueue.Enqueue(evt);
				//Logger.Log("EmitData lock released");
			}
		}

		private void FlushEvent(EEGEvent evt) {
			if (handlers.ContainsKey(evt.type)) {
				//Debug.Log("FlushEvent type=" + evt.type);
				List<DataHandler> h = handlers[evt.type];
				foreach (DataHandler dh in h) {
					//try {
						dh(evt);
					//} catch (Exception e) {
					//	Logger.Error(e);
					//}
				}
			}
		}
	}

	public class RemoteOSCAdapter : EEGDeviceAdapter {
		
		int port;

		UDPListener listener;
		Dictionary<string, EEGDataType> typeMap;
		readonly Converter<object, double> converter = new Converter<object, double>(delegate(object inAdd) {
			// TODO fix this horrendous kludge
			return Double.Parse(inAdd.ToString());
		});

		Thread listenerThread;
		bool stopRequested;

		public RemoteOSCAdapter(int port) : base(4, 220) {
			this.port = port;
		}

		public override void Start() {
			Logger.Log("Starting RemoteOSCAdapter");
			typeMap = InitTypeMap();
			listener = new UDPListener(port);
			listenerThread = new Thread(new ThreadStart(Run));
			listenerThread.Start();
		}

		public override void Stop() {
			Logger.Log("Stopping RemoteOSCAdapter");
			stopRequested = true;
			listenerThread.Join();
			listener.Dispose();
		}

		Dictionary<string, EEGDataType> InitTypeMap() {
			Dictionary<string, EEGDataType> typeMap = new Dictionary<string, EEGDataType>();

			// raw EEG data
			typeMap.Add("/muse/eeg", EEGDataType.EEG);
			//typeMap.Add("/muse/eeg/quantization", EEGDataType.QUANTIZATION);

			// absolute power bands
			typeMap.Add("/muse/elements/alpha_absolute", EEGDataType.ALPHA_ABSOLUTE);
			typeMap.Add("/muse/elements/beta_absolute", EEGDataType.BETA_ABSOLUTE);
			typeMap.Add("/muse/elements/gamma_absolute", EEGDataType.GAMMA_ABSOLUTE);
			typeMap.Add("/muse/elements/delta_absolute", EEGDataType.DELTA_ABSOLUTE);
			typeMap.Add("/muse/elements/theta_absolute", EEGDataType.THETA_ABSOLUTE);

			// relative power bands
			typeMap.Add("/muse/elements/alpha_relative", EEGDataType.ALPHA_RELATIVE);
			typeMap.Add("/muse/elements/beta_relative", EEGDataType.BETA_RELATIVE);
			typeMap.Add("/muse/elements/gamma_relative", EEGDataType.GAMMA_RELATIVE);
			typeMap.Add("/muse/elements/delta_relative", EEGDataType.DELTA_RELATIVE);
			typeMap.Add("/muse/elements/theta_relative", EEGDataType.THETA_RELATIVE);

			// session scores
			//typeMap.Add(EEGDataType.ALPHA_SCORE, "/muse/elements/alpha_session_score");
			//typeMap.Add(EEGDataType.BETA_SCORE, "/muse/elements/beta_session_score");
			//typeMap.Add(EEGDataType.GAMMA_SCORE, "/muse/elements/gamma_session_score");
			//typeMap.Add(EEGDataType.DELTA_SCORE, "/muse/elements/delta_session_score");
			//typeMap.Add(EEGDataType.THETA_SCORE, "/muse/elements/theta_session_score");

			// headband status
			typeMap.Add("/muse/elements/horseshoe", EEGDataType.CONTACT_QUALITY);

			// DRL-REF
			// typeMap.Add(EEGDataType.DRL_REF, "/muse/drlref");

			return typeMap;
		}

		void Run() {
			while (!stopRequested) {
				var packet = listener.Receive();
				if (packet != null)
					OnOSCMessageReceived(packet);
			}
		}

		void OnOSCMessageReceived(OscPacket packet) {
			var msg = (OscMessage) packet;
			if (!typeMap.ContainsKey(msg.Address))
				return;

//			Debug.Log("Got packet from: " + msg.Address);
//			Debug.Log("Arguments: ");
//			foreach (var a in msg.Arguments) {
//				Debug.Log(a.ToString());
//			}

			try {
				var data = msg.Arguments.ConvertAll<double>(converter).ToArray();
				var type = typeMap[msg.Address];

//				Debug.Log("EEGType: " + type);
//				Debug.Log("Converted Args: ");
//				foreach (var d in data) {
//					Debug.Log(d.ToString());
//				}

				EmitData(new EEGEvent(DateTime.UtcNow, type, data));
			} catch (Exception e) {
				Logger.Error("Could not convert/emit data from EEGDeviceAdapter: " + e);
			}
		}
	}

	public struct DummyAdapterSignal {
		public readonly double[] freqs;
		public readonly double[] amplitudes;

		public DummyAdapterSignal(double[] freqs, double[] amplitudes) {
			this.freqs = freqs;
			this.amplitudes = amplitudes;
		}
	}

	public class InstrumentedDummyAdapter : EEGDeviceAdapter {
		public const int SAMPLE_LENGTH = 256 * 10;

		readonly DummyAdapterSignal[] signals;
		readonly double signalToNoiseRatio;

		Thread thread;

		bool isCancelled;

		int currentSignal = -1;

		double[] samples;

		public InstrumentedDummyAdapter(DummyAdapterSignal[] signals, double sampleRate, double signalToNoiseRatio) : base(4, sampleRate) {
			this.signals = signals;
			this.signalToNoiseRatio = signalToNoiseRatio;
			GenerateSamples();
		}

		public void StartSignal(int signal) {
			if (signal < -1 || signal >= signals.Length) throw new ArgumentOutOfRangeException();
			currentSignal = signal;
			GenerateSamples();
		}

		public override void Start() { 
			Logger.Log("Starting DummyAdapter");
			thread = new Thread(Run);
			thread.Start();
		}

		public override void Stop() {
			Logger.Log("Stopping DummyAdapter");
			isCancelled = true;
			thread.Join();			
		}

		void Run() {
			DateTime start = DateTime.UtcNow;
			EmitData(new EEGEvent(start, EEGDataType.CONTACT_QUALITY, new double[] { 1, 1, 1, 1 }));

			// in seconds
			var t = 0;
			int i = 0;
			while (!isCancelled) {
				var v = samples[i++];
				if (i == samples.Length) {
					//Logger.Log("Reached end of samples, getting more noise");
					GenerateSamples();
					i = 0;
				}
				t++;
				EmitData(new EEGEvent(start.AddSeconds(sampleRate* t), EEGDataType.EEG, new double[] { v, v, v, v }));
				Thread.Sleep((int)(Math.Round((1.0 / sampleRate) * 1000)));
			}
		}

		void GenerateSamples() {
			//Logger.Log("Generating samples using currentSample=" + currentSignal);
			var noiseAmplitude = (currentSignal == -1 ? signals.Select((x) => x.amplitudes.Sum()).Average() : signals[currentSignal].amplitudes.Sum()) / signalToNoiseRatio;
			samples = DSP.Generate.NoiseRms(noiseAmplitude, SAMPLE_LENGTH, noiseAmplitude);
			if (currentSignal != -1) {
				var signal = signals[currentSignal];
				for (int i = 0; i < signal.amplitudes.Length; i++) {
					var s = DSP.Generate.ToneSampling(signal.amplitudes[i], signal.freqs[i], sampleRate, SAMPLE_LENGTH, signal.amplitudes[i]);
					samples = DSP.Math.Add(samples, s);
				}
			}
		}
	}


	public class DummyAdapter : InstrumentedDummyAdapter {
		public DummyAdapter(DummyAdapterSignal signal, double sampleRate) : this(signal, sampleRate, 2) { }

		public DummyAdapter(DummyAdapterSignal signal, double sampleRate, double signalToNoise) : base(new DummyAdapterSignal[] { signal }, sampleRate, signalToNoise) {
			StartSignal(0);
		}
	}
}
