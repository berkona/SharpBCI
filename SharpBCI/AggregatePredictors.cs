﻿using System;
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
	 * and then uses an IPredictor<EEGEvent[]> to train/classify on them
	 */
    public class AggregatePredictionPipeable : Pipeable, IPredictorPipeable
    {

        /**
         * Constants used to indicate when the encapsulated predictor is actively predicting
         */
        public const int ID_PREDICT = 0;
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

        EEGEvent[] buffer;

        Dictionary<EEGDataType, int> indexMap;

        public AggregatePredictionPipeable(int channels, int k, double thresholdProb, EEGDataType[] types)
        {
            if (channels <= 0 || k <= 0 || thresholdProb < 0 || thresholdProb >= 1)
                throw new ArgumentOutOfRangeException();

            if (types.Length == 0 || types == null)
                throw new ArgumentOutOfRangeException();
            this.types = types;

            buffer = new EEGEvent[types.Length];

            predictor = new AggregateKNNCorrelationPredictor(channels, k, thresholdProb, types);
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
         * 
         * 
         */
        protected override bool Process(object item)
        {
            EEGEvent evt = (EEGEvent)item;
            if (!types.Contains(evt.type))
                return true;

            if (evt.timestamp != currentTimeStep)
            {
                CheckBufferAndPredict();
                currentTimeStep = evt.timestamp;
            }

            buffer[indexMap[evt.type]] = evt;

            return true;
        }

        void CheckBufferAndPredict()
        {
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i] == null) return;
            }

            if (trainingId == ID_PREDICT)
            {
                var prediction = predictor.Predict(buffer);
                if (prediction != NO_PREDICTION)
                    Logger.Log(string.Format("Predicted: {0}", prediction));
                Add(new TrainedEvent(prediction));
            }
            else
            {
                predictor.AddTrainingData(trainingId, buffer);
            }

            buffer = new EEGEvent[types.Length];
        }
    }

    /**
	 * An IPredictor which uses a 3-dimensional loci of points in the form of an array of EEGEvent's to classify EEG data
	 */
    public abstract class Predictor : IPredictor<EEGEvent[]>
    {

        public const int NO_PREDICTION = -1;

        protected readonly Dictionary<EEGDataType, int> bandLookup = new Dictionary<EEGDataType, int>();
        protected readonly Dictionary<int, List<double[][]>> trainingData = new Dictionary<int, List<double[][]>>();
        protected readonly EEGDataType[] bands;
        protected readonly int channels;
        protected readonly int k;
        protected readonly double thresholdProb;
        protected double[] bandWeights, channelWeights;

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

        public int Predict(EEGEvent[] events)
        {
            var nearestNeighbors = ComputeDistances(TransformToBandSpace(events));

            if (nearestNeighbors == null)
                return NO_PREDICTION;

            int prediction = Vote(nearestNeighbors);

            return prediction;

        }

        public List<KeyValuePair<int, double>> ComputeDistances(double[][] data)
        {
            var distances = new List<KeyValuePair<int, double>>();

            // O(N) * O(|channels|) * O(|bands|) performance
            foreach (var pair in trainingData)
            {
                foreach (var point in pair.Value)
                {
                    double dist = Distance(data, point);
                    distances.Add(new KeyValuePair<int, double>(pair.Key, dist));
                }
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