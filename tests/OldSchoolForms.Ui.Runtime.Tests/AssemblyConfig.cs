using Xunit;

// Konsolen-Capture (JS-Logs) ist global; Parallelisierung würde Ausgaben verwischen.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
