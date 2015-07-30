﻿using SharpLearning.Common.Interfaces;
using SharpLearning.Containers.Extensions;
using SharpLearning.Containers.Matrices;
using SharpLearning.CrossValidation.Samplers;
using SharpLearning.CrossValidation.TrainingValidationSplitters;
using System;
using System.Collections.Generic;

namespace SharpLearning.CrossValidation.BiasVarianceAnalysis
{
    /// <summary>
    /// Bias variance analysis calculator for constructing learning curves.
    /// Learning curves can be used to determine if a model has high bias or high variance.
    /// 
    /// Solutions for model with high bias:
    ///  - Add more features.
    ///  - Use a more sophisticated model
    ///  - Decrease regularization.
    /// Solutions for model with high variance
    ///  - Use fewer features.
    ///  - Use more training samples.
    ///  - Increase Regularization.
    /// </summary>
    public class BiasVarianceLearningCurvesCalculator<TPrediction> : IBiasVarianceLearningCurveCalculator<TPrediction>
    {
        readonly ITrainingValidationIndexSplitter<double> m_trainingValidationIndexSplitter;
        readonly double[] m_samplePercentages;
        readonly IMetric<double, TPrediction> m_metric;
        readonly IIndexSampler<double> m_indexedSampler;
        readonly int m_numberOfShufflesPrSample;
        readonly Random m_random;
        
        /// <summary>
        /// Bias variance analysis calculator for constructing learning curves.
        /// Learning curves can be used to determine if a model has high bias or high variance.
        /// </summary>
        /// <param name="trainingValidationIndexSplitter"></param>
        /// <param name="metric">The error metric used</param>
        /// <param name="samplePercentages">A list of sample percentages determining the 
        /// training data used in each point of the learning curve</param>
        public BiasVarianceLearningCurvesCalculator(ITrainingValidationIndexSplitter<double> trainingValidationIndexSplitter,
            IIndexSampler<double> shuffler, IMetric<double, TPrediction> metric, double[] samplePercentages, int numberOfShufflesPrSample = 5)
        {
            if (trainingValidationIndexSplitter == null) { throw new ArgumentException("trainingValidationIndexSplitter"); }
            if (shuffler == null) { throw new ArgumentException("shuffler"); }
            if (samplePercentages == null) { throw new ArgumentNullException("samplePercentages"); }
            if (samplePercentages.Length < 1) { throw new ArgumentException("SamplePercentages length must be at least 1"); }
            if (metric == null) { throw new ArgumentNullException("metric");}
            if (numberOfShufflesPrSample < 1) { throw new ArgumentNullException("numberOfShufflesPrSample must be at least 1"); }
            
            m_trainingValidationIndexSplitter = trainingValidationIndexSplitter;
            m_indexedSampler = shuffler;
            m_samplePercentages = samplePercentages;
            m_metric = metric;
            m_numberOfShufflesPrSample = numberOfShufflesPrSample;
            m_random = new Random(42);
        }

        /// <summary>
        /// Returns a list of BiasVarianceLearningCurvePoints for constructing learning curves.
        /// The points contain sample size, training score and validation score. 
        /// </summary>
        /// <param name="learnerFactory"></param>
        /// <param name="observations"></param>
        /// <param name="targets"></param>
        /// <returns></returns>
        public List<BiasVarianceLearningCurvePoint> Calculate(IIndexedLearner<TPrediction> learnerFactory,
            F64Matrix observations, double[] targets)
        {
            var trainingValidationIndices = m_trainingValidationIndexSplitter.Split(targets);
            
            return Calculate(learnerFactory, observations, targets,
                trainingValidationIndices.TrainingIndices,
                trainingValidationIndices.ValidationIndices);
        }

        /// <summary>
        /// Returns a list of BiasVarianceLearningCurvePoints for constructing learning curves.
        /// The points contain sample size, training score and validation score. 
        /// </summary>
        /// <param name="learner"></param>
        /// <param name="observations"></param>
        /// <param name="targets"></param>
        /// <param name="trainingIndices">Indices that should be used for training</param>
        /// <param name="validationIndices">Indices that should be used for validation</param>
        /// <returns></returns>
        public List<BiasVarianceLearningCurvePoint> Calculate(IIndexedLearner<TPrediction> learner,
            F64Matrix observations, double[] targets, int[] trainingIndices, int[] validationIndices)
        {
            var learningCurves = new List<BiasVarianceLearningCurvePoint>();

            var validationTargets = targets.GetIndices(validationIndices);
            var validationPredictions = new TPrediction[validationTargets.Length];

            foreach (var samplePercentage in m_samplePercentages)
            {
                if (samplePercentage <= 0.0 || samplePercentage > 1.0)
                { 
                    throw new ArgumentException("Sample percentage must be larger than 0.0 and smaller than or equal to 1.0"); 
                }

                var sampleSize = (int)Math.Round(samplePercentage * (double)trainingIndices.Length);
                if (sampleSize <= 0)
                { 
                    throw new ArgumentException("Sample percentage " + samplePercentage + 
                        " too small for training set size " +trainingIndices.Length); 
                }

                var trainError = 0.0;
                var validationError = 0.0;

                var trainingPredictions = new TPrediction[sampleSize];

                for (int j = 0; j < m_numberOfShufflesPrSample; j++)
                {
                    var sampleIndices = m_indexedSampler.Sample(targets, sampleSize, trainingIndices);
                    var model = learner.Learn(observations, targets, sampleIndices);

                    for (int i = 0; i < trainingPredictions.Length; i++)
                    {
                        trainingPredictions[i] = model.Predict(observations.GetRow(sampleIndices[i]));
                    }

                    for (int i = 0; i < validationIndices.Length; i++)
                    {
                        validationPredictions[i] = model.Predict(observations.GetRow(validationIndices[i]));
                    }

                    var sampleTargets = targets.GetIndices(sampleIndices);
                    trainError += m_metric.Error(sampleTargets, trainingPredictions);
                    validationError += m_metric.Error(validationTargets, validationPredictions);
                }

                trainError = trainError / m_numberOfShufflesPrSample;
                validationError = validationError / m_numberOfShufflesPrSample;
                
                learningCurves.Add(new BiasVarianceLearningCurvePoint(sampleSize,
                    trainError , validationError));
            }

            return learningCurves;
        }
    }
}
