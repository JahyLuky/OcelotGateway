using log4net;
using System.Collections.Concurrent;

namespace OcelotGateway.Services
{
    /// <summary>
    /// Simple circuit breaker implementation for service resilience
    /// </summary>
    public class CircuitBreakerService
    {
        private readonly ILog _logger;
        private readonly ConcurrentDictionary<string, CircuitBreakerState> _circuitStates;
        private readonly int _failureThreshold;
        private readonly TimeSpan _timeout;

        public CircuitBreakerService(int failureThreshold = 5, TimeSpan? timeout = null)
        {
            _logger = LogManager.GetLogger(typeof(CircuitBreakerService));
            _circuitStates = new ConcurrentDictionary<string, CircuitBreakerState>();
            _failureThreshold = failureThreshold;
            _timeout = timeout ?? TimeSpan.FromMinutes(1);
        }

        public async Task<T> ExecuteAsync<T>(string serviceName, Func<Task<T>> operation, T fallbackValue = default(T))
        {
            var state = _circuitStates.GetOrAdd(serviceName, _ => new CircuitBreakerState());

            // Check if circuit is open
            if (state.State == CircuitState.Open)
            {
                if (DateTime.UtcNow - state.LastFailureTime < _timeout)
                {
                    _logger.Debug($"Circuit breaker is OPEN for service '{serviceName}', returning fallback value");
                    return fallbackValue;
                }
                else
                {
                    // Try to transition to half-open
                    state.State = CircuitState.HalfOpen;
                    _logger.Info($"Circuit breaker transitioning to HALF-OPEN for service '{serviceName}'");
                }
            }

            try
            {
                var result = await operation();
                
                // Success - reset or close circuit
                if (state.State == CircuitState.HalfOpen)
                {
                    state.State = CircuitState.Closed;
                    state.FailureCount = 0;
                    _logger.Info($"Circuit breaker CLOSED for service '{serviceName}' after successful operation");
                }
                else if (state.State == CircuitState.Closed && state.FailureCount > 0)
                {
                    state.FailureCount = 0; // Reset failure count on success
                }

                return result;
            }
            catch (Exception ex)
            {
                state.FailureCount++;
                state.LastFailureTime = DateTime.UtcNow;

                if (state.FailureCount >= _failureThreshold)
                {
                    state.State = CircuitState.Open;
                    _logger.Warn($"Circuit breaker OPENED for service '{serviceName}' after {state.FailureCount} failures");
                }
                else
                {
                    _logger.Debug($"Circuit breaker failure {state.FailureCount}/{_failureThreshold} for service '{serviceName}': {ex.Message}");
                }

                // For half-open state, go back to open on failure
                if (state.State == CircuitState.HalfOpen)
                {
                    state.State = CircuitState.Open;
                    _logger.Info($"Circuit breaker returned to OPEN for service '{serviceName}' after half-open failure");
                }

                return fallbackValue;
            }
        }

        public CircuitState GetCircuitState(string serviceName)
        {
            return _circuitStates.TryGetValue(serviceName, out var state) ? state.State : CircuitState.Closed;
        }

        public void ResetCircuit(string serviceName)
        {
            if (_circuitStates.TryGetValue(serviceName, out var state))
            {
                state.State = CircuitState.Closed;
                state.FailureCount = 0;
                _logger.Info($"Circuit breaker manually reset for service '{serviceName}'");
            }
        }
    }

    public class CircuitBreakerState
    {
        public CircuitState State { get; set; } = CircuitState.Closed;
        public int FailureCount { get; set; } = 0;
        public DateTime LastFailureTime { get; set; } = DateTime.MinValue;
    }

    public enum CircuitState
    {
        Closed,   // Normal operation
        Open,     // Failing, not allowing requests
        HalfOpen  // Testing if service is back up
    }
}