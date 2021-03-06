using System;
using System.Collections.Generic;
using System.Linq;

namespace SharpBCI
{

    /**
	 * A class for statistical utility functions.
	 * Most are fairly naive in their calculation so not very performant.
	 */
    public static class StatsUtils
    {

        public static double SampleMean(double[] x)
        {
            if (x == null || x.Length == 0)
                throw new ArgumentOutOfRangeException();

            return x.Average();
        }

        public static double SampleVar(double[] x)
        {
            return SampleVar(x, SampleMean(x));
        }

        public static double SampleVar(double[] x, double mu)
        {
            if (x == null || x.Length < 2)
                throw new ArgumentOutOfRangeException();

            return x.Select((xi) => (xi - mu) * (xi - mu)).Sum() / (x.Length - 1);
        }

        public static double SampleStd(double[] x)
        {
            if (x == null || x.Length == 0)
                throw new ArgumentOutOfRangeException();
            return Math.Sqrt(SampleVar(x));
        }

        public static double SampleCov(double[] x, double[] y)
        {
            if (x == null || x.Length == 0 || x.Length != y.Length)
                throw new ArgumentOutOfRangeException();

            double xAvg = SampleMean(x);
            double yAvg = SampleMean(y);

            return x.Zip(y, (xi, yi) => (xi - xAvg) * (yi - yAvg)).Sum() / (x.Length-1);
        }

        public static double Corr(double[] x, double[] y)
        {
            if (x == null || x.Length == 0 || x.Length != y.Length)
                throw new ArgumentOutOfRangeException();
           
            double xAvg = SampleMean(x);
            double yAvg = SampleMean(y);

            double numerator = x.Zip(y, (xi, yi) => (xi - xAvg) * (yi - yAvg)).Sum();

            double xSumSq = x.Sum(i => Math.Pow((i - xAvg), 2));
            double ySumSq = y.Sum(i => Math.Pow((i - yAvg), 2));

            double denominator = Math.Sqrt(xSumSq * ySumSq);

            return numerator / denominator;
        }

        public static double WeightedMean(double[] x, double[] b)
        {

            if (x == null || b == null || x.Length == 0 || x.Length != b.Length)
                throw new ArgumentOutOfRangeException();

            double weightSum = b.Sum();
            return x.Zip(b, (xi, wi) => (xi * wi)).Sum() / weightSum;
        }

        public static double WeightedCovariance(double[] x, double[] y, double[] b)
        {

            if (x.Length == 0 || x.Length != y.Length || y.Length != b.Length)
                throw new ArgumentOutOfRangeException();

            double weightSum = b.Sum();
            double wxAvg = WeightedMean(x, b);
            double wyAvg = WeightedMean(y, b);

            return x.Zip(y, (xi, yi) => (xi - wxAvg) * (yi - wyAvg))
                    .Zip(b, (i, j) => (i * j))
                    .Sum() / weightSum;
        }

        public static double WeightedCorrelation(double[] x, double[] y, double[] b)
        {
            if (x.Length == 0 || x.Length != y.Length || y.Length != b.Length)
                throw new ArgumentOutOfRangeException();

            var numerator = WeightedCovariance(x, y, b);
            var denominator = Math.Sqrt(WeightedCovariance(x, x, b) * WeightedCovariance(y, y, b));

            return numerator / denominator;
        }

        public static string Summary(double[] x)
        {
            return string.Format("double[\nn: {0}, mean: {1}, std. deviation: {2}\nfirst 5: {3}\nlast 5: {4}\n]",
                                 x.Length, SampleMean(x), Math.Sqrt(SampleVar(x)),
                                 string.Join(", ", x.Take(5)),
                                 string.Join(", ", x.Reverse().Take(5))
                                );
        }

        /**
		 * Calculate ACF(k|x, mu, sigmaSq)
		 * Assumes the mean and variance of the population of X is known
		 */
        public static double ACF(int k, double[] x)
        {
            var mu = SampleMean(x);
            var n = x.Length;
            var cov = 0.0;
            for (uint t = 0; t < n - k; t++)
            {
                cov += (x[t] - mu) * (x[t + k] - mu);
            }
            cov /= n;
            var sigma = x.Select((xi) => (xi - mu) * (xi - mu)).Sum() / n;
            return cov / sigma;
        }

        /**
		 * Find a value p such that PACF(i) for all i > p+1 is w/in 5% CI interval of zero
		 * Approximate performance = O(N^3) * maxOrder
		 */
        public static uint EstimateAROrder(double[] x, uint maxOrder)
        {
            //var p = x.Length / 4;
            double[] pacf = new double[maxOrder];
            for (uint i = 1; i <= maxOrder; i++)
            {
                // pacf(i) = pacf[i-1]
                pacf[i - 1] = PACF(i, x);
            }

            var pacf_bar = SampleMean(pacf);
            var pacf_s = Math.Sqrt(SampleVar(pacf, pacf_bar));

            var minCutoff = pacf_bar - 2 * pacf_s;
            var maxCutoff = pacf_bar + 2 * pacf_s;

            //Logger.Log("PACF mean = {0}, s = {1}, 95% CI = [{2}, {3}]", pacf_bar, pacf_s, minCutoff, maxCutoff);
            //Logger.Log("PACF = {0}", string.Join(", ", pacf));

            for (uint i = 1; i < maxOrder; i++)
            {
                // find such an i in [1, p] that pacf(i+1) is zero w/in 5% CI
                if (pacf.Skip((int)i).All((pacf_i) => pacf_i >= minCutoff && pacf_i <= maxCutoff))
                    return i;
            }
            // error case, throw exception??
            return maxOrder;
        }

        /**
		 * Calculate PACF(k|x)
		 * Uses a Yule-Walker Estimation of AR model, so is atleast O(N^3)
		 */
        public static double PACF(uint k, double[] x)
        {
            return FitAR(k, x)[k - 1];
        }

        /**
		 * Try to fit an AR(p) model to x using Yule-Walker Estimation
		 * Since AR(0) = noise, does not support p == 0
		 */
        public static double[] FitAR(uint p, double[] x)
        {
            if (p <= 0)
                throw new ArgumentOutOfRangeException();

            var x_bar = SampleMean(x);
            var s_sq = SampleVar(x, x_bar);

            // Yule-Walker fitting

            // a column vector of auto-correlations from 1 to p
            double[][] r = MatrixUtils.Create((int)p, 1);
            for (int i = 1; i <= p; i++)
            {
                r[i - 1][0] = ACF(i, x);
            }

            //Console.WriteLine("r:\n{0}", MatrixUtils.ToString(r));

            // construct R, a system of equations to solve the PHI vector given r
            // p = 3 should look like
            // [ r0, r1, r2 ] 
            // [ r1, r0, r1 ]
            // [ r2, r1, r0 ]
            // note r0 = 1
            var R = MatrixUtils.Create((int)p, (int)p);
            for (int i = 0; i < p; i++)
            {
                int k = i;
                int step = -1;
                for (int j = 0; j < p; j++)
                {
                    // accor(0) = 1
                    if (k == 0)
                    {
                        R[i][j] = 1;
                        step = 1;
                    }
                    // acorr(x) = r[x-1]
                    else
                    {
                        R[i][j] = r[k - 1][0];
                    }
                    k += step;
                }
            }
            //Console.WriteLine("R:\n{0}", MatrixUtils.ToString(R));
            // invert R to solve for phi
            R = MatrixUtils.Inverse(R);
            //Console.WriteLine("R^-1:\n{0}", MatrixUtils.ToString(R));

            // R = phi * r => phi = R^-1 * r
            // R is a p x p matrix and r is a p x 1 matrix so result is p x 1 matrix (a column vector)
            var phi = MatrixUtils.Product(R, r);
            //Console.WriteLine("phi:\n{0}", MatrixUtils.ToString(phi));
            // we want a normal array (a sort of row vector) for portability so transpose the resulting column vector
            return MatrixUtils.Transpose(phi)[0];
        }
    }

    /**
	 * Simultaneously calculates sample mean and variance in O(1)
	 * using Welford-Knuth online algorithm.
	 */
    public class OnlineVariance
    {
        public double mean { get { return _mean; } }
        public double var { get { return _var; } }
        public bool isValid { get { return n >= 2; } }

        uint n = 0;
        double _mean = 0.0;
        double _mean2 = 0.0;

        double _var = double.NaN;

        public void Update(double x)
        {
            // Welford-Knuth online variance algorithm
            n++;
            double delta = x - _mean;
            _mean += delta / n;
            double delta2 = x - mean;
            _mean2 += delta * delta2;

            if (n >= 2)
                _var = _mean2 / (n - 1);
        }
    }

    /**
	 * A simple AR model class which can predict the next value of X given 
	 * AR parameters (phi), a constant factor (i.e., E[noise(X)]), and previous values of X
	 * O(p) performance
	 */
    public class ARModel
    {

        readonly IndexableQueue<double> previousValues = new IndexableQueue<double>();

        readonly double c;
        readonly double[] parameters;

        public ARModel(double c, double[] parameters)
        {
            if (double.IsNaN(c) || double.IsInfinity(c) || parameters == null || parameters.Length == 0)
                throw new ArgumentOutOfRangeException();

            this.c = c;
            this.parameters = parameters;
        }

        public double Predict(double x)
        {
            // default to x
            double x_hat = x;

            previousValues.Enqueue(x);

            // we have sufficient data to allow x_hat to be defined
            if (previousValues.Count == parameters.Length + 1)
            {
                previousValues.Dequeue();
                x_hat = 0;
                for (int i = parameters.Length - 1; i >= 0; i--)
                {
                    x_hat += parameters[i] * previousValues[i];
                }
                x_hat += c;
            }

            return x_hat;
        }
    }
}