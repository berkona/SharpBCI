using System;
using System.Linq;
using System.Collections.Generic;

using Accord.Neuro.Networks;
using Accord.Neuro;
using Accord.Neuro.Learning;

using System.Threading;
using System.Threading.Tasks;

using System.Collections.Concurrent;

using SharpBCI;

namespace SharpBCI
{
    public class DBNPredictionPipeable : Pipeable, IPredictorPipeable
    {

        public static int ID_PREDICT = 0;
        public static int NUM_UNSUPERVISED_TRAINING = 500;

        DeepBeliefNetworkPredictor predictor;
        int trainingId = ID_PREDICT;
        DateTime currentTimeStep;
        EEGEvent[] buffer;
        EEGDataType[] types;
        Dictionary<EEGDataType, int> indexMap;

        public DBNPredictionPipeable(int channels, object[] typeNames)
            : this(channels, typeNames.Select((x) => (EEGDataType)Enum.Parse(typeof(EEGDataType), (string)x)).ToArray())
        {

        }

        public DBNPredictionPipeable(int channels, EEGDataType[] types)
        {
            if (channels <= 0)
                throw new ArgumentOutOfRangeException();

            if (types.Length == 0 || types == null)
                throw new ArgumentOutOfRangeException();
            this.types = types;

            buffer = new EEGEvent[types.Length];

            predictor = new DeepBeliefNetworkPredictor(channels, types);
            indexMap = new Dictionary<EEGDataType, int>();
            for (int i = 0; i < types.Length; i++)
            {
                indexMap.Add(types[i], i);
            }
        }

        public void ClearTrainingData()
        {
            predictor.ClearTrainingData();
        }

        public void StartTraining(int id)
        {
            if (trainingId != ID_PREDICT)
                throw new InvalidOperationException("Training already started");
            trainingId = id;
        }


        public void StopTraining(int id)
        {
            if (trainingId == ID_PREDICT)
                throw new InvalidOperationException("No training started");
            trainingId = ID_PREDICT;
        }

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

        int unsupervised_count = 0;

        void CheckBufferAndPredict()
        {
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i] == null) return;
            }

            if (unsupervised_count <= NUM_UNSUPERVISED_TRAINING)
            {
                predictor.Look(buffer);
            }

            if (trainingId == ID_PREDICT)
            {
                var prediction = predictor.Predict(buffer);
                if (prediction != -1)
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

    public class DeepBeliefNetworkPredictor : IPredictor<EEGEvent[]>
    {

        DeepBeliefNetwork network;
        DeepBeliefNetworkLearning uteacher;
        BackPropagationLearning steacher;

        static int MAX_NUM_LABELS = 10;

        int channels;
        EEGDataType[] bands;


        public DeepBeliefNetworkPredictor(int channels, EEGDataType[] bands)
        {
            if (channels <= 0)
                throw new ArgumentOutOfRangeException();
            if (channels > MAX_NUM_LABELS)
                throw new ArgumentOutOfRangeException(
                    string.Format("DBN does not supprt more than {0} classes", MAX_NUM_LABELS));

            if (bands == null || bands.Length == 0)
                throw new ArgumentOutOfRangeException();
            
            this.channels = channels;
            this.bands = bands;
            InitializeNetwork();
        }

        private void InitializeNetwork() {
            network = new DeepBeliefNetwork(channels * bands.Length, 10, 10);
            new GaussianWeights(network, 0.1).Randomize();
            network.UpdateVisibleWeights();

            uteacher = new DeepBeliefNetworkLearning(network)
            {
                Algorithm = (h, v, i) => new ContrastiveDivergenceLearning(h, v)
                {
                    LearningRate = 0.1,
                    Momentum = 0.5,
                    Decay = 0.001,
                }
            };

            steacher = new BackPropagationLearning(network)
            {
                LearningRate = 0.1,
                Momentum = 0.5
            };
        }

        private double[] Flatten(EEGEvent[] data)
        {
            // transform to a set of points in band-space
            double[][] points = new double[channels][];
            for (int i = 0; i < points.Length; i++)
                points[i] = new double[bands.Length];

            foreach (EEGEvent evt in data)
            {
                for (int cIdx = 0; cIdx < evt.data.Length; cIdx++)
                {
                    points[cIdx] = evt.data;
                }
            }
            double[] flattened = points.SelectMany(a => a).ToArray();

            return flattened;
        }

        public void AddTrainingData(int label, EEGEvent[] data)
        {
            double[][] input = new double[1][];
            input[0] = Flatten(data);

            double[][] output = new double[1][];
            output[0] = FormatOutputVector(label, MAX_NUM_LABELS);

            steacher.RunEpoch(input, output);
        }

        public void AddTrainingData(int label, double[] data) {
            double[][] input = new double[1][];
            input[0] = data;

            double[][] output = new double[1][];
            output[0] = FormatOutputVector(label, MAX_NUM_LABELS);

            steacher.RunEpoch(input, output);
        }


        public void ClearTrainingData()
        {
            InitializeNetwork();
        }

        public int Predict(EEGEvent[] test)
        {
            return FormatOutputResult(network.Compute(Flatten(test)));
        }

        public int Predict(double[] test) 
        {
            return FormatOutputResult(network.Compute(test));
        }

        public double Look(EEGEvent[] data)
        {
            double[][] input = new double[1][];
            input[0] = Flatten(data);
            return uteacher.RunEpoch(input);
        }

        public double Look(double[] data)
        {
            double[][] input = new double[1][];
            input[0] = data;
            return uteacher.RunEpoch(input);
        }

        public static double[] FormatOutputVector(double label, int num_labels)
        {
            double[] output = new double[num_labels];

            for (int i = 0; i < output.Length; i++)
            {
                output[i] = Math.Abs(i - label) < 1e-5 ? 1 : 0;
            }

            return output;
        }

        public static int FormatOutputResult(double[] output)
        {
            return output.ToList().IndexOf(output.Max());
        }

    }

}




