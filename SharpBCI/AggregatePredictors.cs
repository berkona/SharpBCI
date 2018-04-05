using System;
using System.Linq;
using System.Collections.Generic;

namespace SharpBCI
{
    /**
     * Interface for all predictors. All current predictors take double[] as data.
     * Should be encapsulated in an IPredictorPipeable.
     * @see IPredictorPipeable
     */
    public interface IPredictor<T>
    {
        /**
         * Add data to labeled training data set. The collection of trained
         * data is used to create a model for Predict.
         * @see Predict(T test)
         * @param label - a unique non-negative non-zero integer which identifies the label being trained on
         * @param data - training data for predictor. Generally double[]
         */
        void AddTrainingData(int label, T data);

        /**
         * Clears all training data stored within the predictor
         * Essentially a reset of the entire prediction system
         * Use between changing environments and/or participants
         */
        void ClearTrainingData();

        /**
         * Makes a prediction from the underlying model based off the new data
         * @see AddTrainingData(int label, T data)
         * @param test - data to be predicted on. Must be the same type/shape/format of AddTrainingData data
         * @return int the corresponds with the label of the predicted class
         */
        int Predict(T test);
    }

    /**
     * Extends the typical pipeable to include training controls
     * SharpBCI uses these functions to interface with the prediction mechanisms
     * @see IPipeable
     * @see IPredictor<T>
     */
    public interface IPredictorPipeable : IPipeable
    {
        /**
         * Starts training data collection on a particular id (label)
         * Should continue training on that id until StopTraining() is called on the same id
         */
        void StartTraining(int id);

        /**
         * Ends training data collection on a particular id (label)
         * Must be paired with a preceding StartTraining on that label
         */
        void StopTraining(int id);

        /**
         * Clears all training data stored within the encapsulated predictor
         * Essentially a reset of the entire prediction system
         * Use between changing environments and/or participants
         */
        void ClearTrainingData();
    }

    /**
	 * A pipeable which aggregates received EEGEvents into an array based on the reported EEGEvent timestamp
	 * and then uses an IPredictor<EEGEvent[]> to train/classify on them. A TrainedEvent will be pushed
	 * on to the next stage.
     * @see EEGEvent
     * @see TrainedEvent
	 */
    public class AggregatePredictionPipeable : Pipeable, IPredictorPipeable
    {

        /**
         * Constant used to indicate when the encapsulated predictor is actively predicting
         */
        public const int ID_PREDICT = 0;
        /**
         * Constant used to indicate when the encapsulated predictor has no valid prediction
         */
        public const int NO_PREDICTION = -1;

        /**
         * Ensure predictor starts in predicting mode as there is no trained data to predict on
         */
        int trainingId = ID_PREDICT;

        /**
         * Encapsulated predictor which the Pipeable will pass all data to and recieve predictions from
         * For more information @see Predictor
         * For an example predictor @see AggregateKNNCorrelationPredictor
         */
        IPredictor<EEGEvent[]> predictor;

        /*
         * List of EEGDataTypes that the predictor expects for its training and predictions
         */
        EEGDataType[] types;

        /**
         * Records the time of incoming EEGEvents. Multiple EEGEvents will come for each timestamp
         * (one for each type). An incoming EEGEvent with a newer timestamp indicates the previous buffer is full.
         */
        DateTime currentTimeStep;

        /**
         * Buffer of incoming events with the same timestamp that will be passed to the IPredictor<EEGEvent[]> when
         * full/when a more currently timestamped EEGEvent is recieved.
         */
        EEGEvent[] buffer;

        /**
         * Dictionary which maps EEGDataTypes to incrementing integers for easy indexing into the buffer
         */
        Dictionary<EEGDataType, int> indexMap;

        /*
         * param channels - number of channels on the headset, as reported by @SharpBCIAdapter
         * param k - a non-negative non-zero integer representing the number of results from KNN algorithm. @link https://en.wikipedia.org/wiki/K-nearest_neighbors_algorithm
         * param thesholdProb - a double between 0-1 indicating the probability threshhold below which predictions will be thrown out
         * param type - EEGDataType[] indicating which types the predictor is interested in (and will use in its prediction)
         */

        public AggregatePredictionPipeable(int channels, int k, double thresholdProb, EEGDataType[] types)
        {
            if (channels <= 0 || k <= 0 || thresholdProb < 0 || thresholdProb >= 1)
                throw new ArgumentOutOfRangeException();

            if (types.Length == 0 || types == null)
                throw new ArgumentOutOfRangeException();

            predictor = new AggregateKNNCorrelationPredictor(channels, k, thresholdProb, types);
            
            this.types = types;

            buffer = new EEGEvent[types.Length];

            indexMap = new Dictionary<EEGDataType, int>();
            for (int i = 0; i < types.Length; i++)
            {
                indexMap.Add(types[i], i);
            }
        }

        public AggregatePredictionPipeable(int channels, int k, double thresholdProb, object[] typeNames)
            : this(channels, k, thresholdProb, typeNames.Select((x) => (EEGDataType)Enum.Parse(typeof(EEGDataType), (string)x)).ToArray())
        {

        }

        /**
         * Clears all training data stored within the predictor
         * Essentially a reset of the entire prediction system
         * Use between changing environments and/or participants
         */
        public void ClearTrainingData()
        {
            predictor.ClearTrainingData();
        }

        /**
         * Start training predictor on the EEG data from now on
         * Should be paired w/ a StopTraining(id) call
         * @param id - a unique non-negative non-zero integer which identifies this trained event
         */
        public void StartTraining(int id)
        {
            if (trainingId != ID_PREDICT)
                throw new InvalidOperationException("Training already started");
            
            trainingId = id;
        }

        /**
         * Stop training predictor on the EEG data from now on
         * Should be paired w/ a StopTraining(id) call
         * @param id - a unique non-negative non-zero integer which identifies this trained event
         */
        public void StopTraining(int id)
        {
            if (trainingId == ID_PREDICT)
                throw new InvalidOperationException("No training in progress");
            
            if (id != trainingId)
                throw new InvalidOperationException("Attempting to call StopTraining on an inactive id");

            trainingId = ID_PREDICT;
        }

        /**
         * Collects incoming EEGEvents (one per type), and stores them in buffer. If the incoming
         * event's timestamp is new, attempt to send the buffer onto the IPredictor<EEGEvent[]>
         * If the current trainingId is non-zero, the data will be used for training on the trainingId label
         * If the current trainingId is zero, the data will be predicted on
         * @see Pipeable
         */
        protected override bool Process(object item)
        {
            //Incoming objects must be EEGEvent
            EEGEvent evt = (EEGEvent)item;

            //Also must be a type we care about
            if (!types.Contains(evt.type))
                return true;

            //If we start getting data from a new time, send it to the predictor
            if (evt.timestamp != currentTimeStep)
            {
                CheckBufferAndPredict();
                currentTimeStep = evt.timestamp;
            }

            //Add the new event into the buffer
            buffer[indexMap[evt.type]] = evt;

            return true;
        }

        void CheckBufferAndPredict()
        {
            //Ensure the buffer is full
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i] == null) return;
            }


            //If the predictor is in its prediction state, predict and pass on the result
            if (trainingId == ID_PREDICT)
            {
                var prediction = predictor.Predict(buffer);
                if (prediction != NO_PREDICTION)
                {
                    Logger.Log(string.Format("Predicted: {0}", prediction));
                    Add(new TrainedEvent(prediction));
                }
            }
            //Otherwise add the data to the training data set at the corresponding label
            else
            {
                predictor.AddTrainingData(trainingId, buffer);
            }

            //Reset the buffer
            buffer = new EEGEvent[types.Length];
        }
    }

    /**
	 * An IPredictor which uses a 3-dimensional loci of points in the form of an array of EEGEvent's to classify EEG data
	 * Uses nearest neighbor predictions with distance computed by the abstract Compute(double[] x, double[] y) function
	 * Predictions occur by computing the distance between the incoming sample and all training samples, pulls the k
	 * nearest neighbors, and determines the final prediction
	 */
    public abstract class Predictor : IPredictor<EEGEvent[]>
    {

        /**
         * see AggregatePredictionPipeable.NO_PREDICTION
         */
        public const int NO_PREDICTION = -1;

        /**
         * Dictionary which maps EEGDataTypes to integers for easy indexing into the buffer
         */
        protected readonly Dictionary<EEGDataType, int> bandLookup = new Dictionary<EEGDataType, int>();

        /**
         * Dictionary storing all training data. Key is the label of the value's data
         * Value is a list of data associated with this label
         */
        protected readonly Dictionary<int, List<double[][]>> trainingData = new Dictionary<int, List<double[][]>>();

        /**
         * List of EEGDataTypes that the predictor expects for its training and predictions
         */
        protected readonly EEGDataType[] bands;

        /** 
         * Number of channels on the headset, as reported by @see SharpBCIAdapter
         */
        protected readonly int channels;

        /** 
         * A non-negative non-zero integer representing the number of results from KNN algorithm.
         * @link https://en.wikipedia.org/wiki/K-nearest_neighbors_algorithm
         */
        protected readonly int k;

        /**
         * A double between 0-1 indicating the probability threshhold below which predictions will be thrown out
         */
        protected readonly double thresholdProb;

        protected double[] bandWeights;
        protected double[] channelWeights;

        public Predictor(int channels, int k, double thresholdProb, EEGDataType[] bands)
        {

            if (channels <= 0 || k <= 0 || thresholdProb < 0 || thresholdProb >= 1)
                throw new ArgumentOutOfRangeException();

            if (bands == null || bands.Length == 0)
                throw new ArgumentOutOfRangeException();

            this.channels = channels;
            this.channelWeights = Enumerable.Repeat<double>(1, channels).ToArray();
            this.bands = bands;
            this.bandWeights = Enumerable.Repeat<double>(1, bands.Length).ToArray();
            this.k = k;
            this.thresholdProb = thresholdProb;

            for (int i = 0; i < bands.Length; i++)
            {
                bandLookup.Add(bands[i], i);
            }
        }

        public void AddTrainingData(int id, EEGEvent[] events)
		{
			if (!trainingData.ContainsKey(id))
				trainingData.Add(id, new List<double[][]>());
			trainingData[id].Add(TransformToBandSpace(events));
        }

        public void ClearTrainingData()
        {
			trainingData.Clear();
        }

        public void SetChannelWeights(double[] newWeights) {
            if(newWeights ==null || newWeights.Length != channelWeights.Length) {
                throw new ArgumentOutOfRangeException();
            }

            channelWeights = newWeights;
        }

        public void SetBandWeights(double[] newWeights)
        {
            if (newWeights == null || newWeights.Length != bandWeights.Length)
            {
                throw new ArgumentOutOfRangeException();
            }

            bandWeights = newWeights;
        }

        public int Predict(EEGEvent[] events)
        {
            var nearestNeighbors = ComputeDistances(TransformToBandSpace(events));

            if (nearestNeighbors == null)
                return NO_PREDICTION;

            int prediction = Vote(nearestNeighbors);

            return prediction;

        }

        public List<KeyValuePair<int, double>> ComputeDistances(double[][] data) {
            var distances = new List<KeyValuePair<int, double>>();

			// O(N) * O(|channels|) * O(|bands|) performance
			try {
				foreach (var pair in trainingData) {
					foreach (var point in pair.Value) {
						double dist = Distance(data, point);
						distances.Add(new KeyValuePair<int, double>(pair.Key, dist));
					}
				}
			} catch (InvalidOperationException) {
				// trainingData was modified out from underneath of us,
				// return null prediction
				return null;
			}

            if (distances.Count == 0)
                return null;

            return distances.ToList();
        }



        public int Vote(List<KeyValuePair<int, double>> distances)
        {
            var nearestNeighbors = distances.OrderByDescending((x) => x.Value).Take(k);

            // use a plurality voting system weighted by distance from us
            double voteSum = 0;
            Dictionary<int, double> votes = new Dictionary<int, double>();
            foreach (var neighbor in nearestNeighbors)
            {
                if (!votes.ContainsKey(neighbor.Key))
                    votes.Add(neighbor.Key, 0);
                var vote = 1.0 / neighbor.Value;
                votes[neighbor.Key] += vote;
                voteSum += vote;
            }

            var winner = votes.OrderBy((x) => x.Value).First();
            return winner.Value / voteSum > thresholdProb ? winner.Key : NO_PREDICTION;
        }


        double[][] TransformToBandSpace(EEGEvent[] events)
        {
            // transform to a set of points in band-space
            double[][] points = new double[channels][];
            for (int i = 0; i < points.Length; i++)
                points[i] = new double[bands.Length];

            foreach (EEGEvent evt in events)
            {
                var bIdx = bandLookup[evt.type];
                for (int cIdx = 0; cIdx < evt.data.Length; cIdx++)
                {
                    points[cIdx][bIdx] = evt.data[cIdx];
                }
            }
            return points;
        }

        double Distance(double[][] a, double[][] b)
        {
            double dist = 0;
            for (int channel = 0; channel < channels; channel++)
            {
                dist += ((Compute(a[channel], b[channel]) + 1) * channelWeights[channel]) / bands.Length;
            }
            dist /= channels;
            return dist;
        }

        protected abstract double Compute(double[] x, double[] y);
    }

    public class AggregateKNNCorrelationPredictor : Predictor
    {

        public AggregateKNNCorrelationPredictor(int channels, int k, double thresholdProb, EEGDataType[] bands)
            : base(channels, k, thresholdProb, bands) { }

        protected override double Compute(double[] x, double[] y)
        {
            return StatsUtils.WeightedCorrelation(x, y, bandWeights);
        }
    }

    public class AggregateKNNDTWPredictor : Predictor
    {
        public AggregateKNNDTWPredictor(int channels, int k, double thresholdProb, EEGDataType[] bands)
            : base(channels, k, thresholdProb, bands) { }


        protected override double Compute(double[] x, double[] y)
        {
            return new DynamicTimeWarping(x, y).GetCost();
        }
    }
}