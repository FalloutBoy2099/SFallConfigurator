using Xunit;

// The language, the settings folder and the theme are process wide: tests that change them must not overlap with others.
[assembly: CollectionBehavior(DisableTestParallelization = true)]