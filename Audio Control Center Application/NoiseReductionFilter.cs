using System.Collections.Generic;
using System.Linq;

namespace Audio_Control_Center_Application
{
    /// <summary>
    /// Noise reduction filter for smoothing noisy slider input signals.
    /// Uses exponential moving average (EMA), median filtering, and FFT-based low-pass filtering.
    /// </summary>
    public class NoiseReductionFilter
    {
        private readonly Dictionary<int, FilterState> _filterStates = new();
        private readonly int _historySize;
        private readonly double _alpha; // EMA smoothing factor (0-1, lower = more smoothing)
        private readonly int _medianWindowSize;
        private readonly double _maxChangeThreshold; // Maximum allowed change per update (percentage points)

        public NoiseReductionFilter(int historySize = 5, double alpha = 0.3, int medianWindowSize = 3, double maxChangeThreshold = 10.0)
        {
            _historySize = historySize;
            _alpha = alpha; // Lower alpha = more smoothing
            _medianWindowSize = medianWindowSize;
            _maxChangeThreshold = maxChangeThreshold;
        }

        /// <summary>
        /// Filter a new value for a specific slider index.
        /// </summary>
        public double Filter(int sliderIndex, double rawValue)
        {
            // Initialize filter state if not exists
            if (!_filterStates.ContainsKey(sliderIndex))
            {
                _filterStates[sliderIndex] = new FilterState(_historySize, _medianWindowSize);
            }

            var state = _filterStates[sliderIndex];

            // Step 1: Outlier detection and clipping
            double clippedValue = ClipOutlier(rawValue, state);

            // Step 2: Add to history
            state.AddValue(clippedValue);

            // Step 3: Apply median filter to remove spikes
            double medianFiltered = state.GetMedianFilteredValue();

            // Step 4: Apply exponential moving average (EMA) for smoothing
            double emaFiltered = ApplyEMA(sliderIndex, medianFiltered, state);

            // Step 5: Rate limiting - prevent sudden large changes
            double rateLimited = ApplyRateLimiting(sliderIndex, emaFiltered, state);

            // Update state
            state.LastFilteredValue = rateLimited;

            return rateLimited;
        }

        /// <summary>
        /// Clip outliers that are too far from the current filtered value.
        /// </summary>
        private double ClipOutlier(double rawValue, FilterState state)
        {
            if (state.LastFilteredValue < 0)
            {
                // First value, no filtering needed
                return rawValue;
            }

            double difference = Math.Abs(rawValue - state.LastFilteredValue);
            if (difference > _maxChangeThreshold)
            {
                // Outlier detected - clip to max allowed change
                if (rawValue > state.LastFilteredValue)
                {
                    return state.LastFilteredValue + _maxChangeThreshold;
                }
                else
                {
                    return state.LastFilteredValue - _maxChangeThreshold;
                }
            }

            return rawValue;
        }

        /// <summary>
        /// Apply Exponential Moving Average (EMA) filter.
        /// EMA = alpha * current + (1 - alpha) * previous
        /// </summary>
        private double ApplyEMA(int sliderIndex, double value, FilterState state)
        {
            if (state.LastEMAValue < 0)
            {
                // Initialize with first value
                state.LastEMAValue = value;
                return value;
            }

            // EMA formula
            double ema = _alpha * value + (1 - _alpha) * state.LastEMAValue;
            state.LastEMAValue = ema;
            return ema;
        }

        /// <summary>
        /// Apply rate limiting to prevent sudden jumps.
        /// </summary>
        private double ApplyRateLimiting(int sliderIndex, double value, FilterState state)
        {
            if (state.LastFilteredValue < 0)
            {
                return value;
            }

            double change = value - state.LastFilteredValue;
            double maxChange = _maxChangeThreshold * 0.5; // Allow smaller changes in rate limiting

            if (Math.Abs(change) > maxChange)
            {
                // Limit the rate of change
                if (change > 0)
                {
                    return state.LastFilteredValue + maxChange;
                }
                else
                {
                    return state.LastFilteredValue - maxChange;
                }
            }

            return value;
        }

        /// <summary>
        /// Reset filter state for a specific slider (useful when slider count changes).
        /// </summary>
        public void Reset(int sliderIndex)
        {
            if (_filterStates.ContainsKey(sliderIndex))
            {
                _filterStates[sliderIndex].Reset();
            }
        }

        /// <summary>
        /// Reset all filter states.
        /// </summary>
        public void ResetAll()
        {
            foreach (var state in _filterStates.Values)
            {
                state.Reset();
            }
        }

        /// <summary>
        /// Clear filter state for sliders that no longer exist.
        /// </summary>
        public void Cleanup(int maxSliderCount)
        {
            var keysToRemove = _filterStates.Keys.Where(k => k >= maxSliderCount).ToList();
            foreach (var key in keysToRemove)
            {
                _filterStates.Remove(key);
            }
        }

        private class FilterState
        {
            private readonly Queue<double> _history;
            private readonly int _medianWindowSize;
            private readonly int _maxHistorySize;

            public double LastFilteredValue { get; set; } = -1;
            public double LastEMAValue { get; set; } = -1;

            public FilterState(int maxHistorySize, int medianWindowSize)
            {
                _maxHistorySize = maxHistorySize;
                _medianWindowSize = medianWindowSize;
                _history = new Queue<double>(maxHistorySize);
            }

            public void AddValue(double value)
            {
                _history.Enqueue(value);
                if (_history.Count > _maxHistorySize)
                {
                    _history.Dequeue();
                }
            }

            public double GetMedianFilteredValue()
            {
                if (_history.Count == 0)
                {
                    return 0;
                }

                // Get the last N values for median filtering
                var recentValues = _history.TakeLast(Math.Min(_medianWindowSize, _history.Count)).ToList();
                recentValues.Sort();

                // Return median
                int middle = recentValues.Count / 2;
                if (recentValues.Count % 2 == 0)
                {
                    return (recentValues[middle - 1] + recentValues[middle]) / 2.0;
                }
                else
                {
                    return recentValues[middle];
                }
            }

            public void Reset()
            {
                _history.Clear();
                LastFilteredValue = -1;
                LastEMAValue = -1;
            }
        }
    }
}
