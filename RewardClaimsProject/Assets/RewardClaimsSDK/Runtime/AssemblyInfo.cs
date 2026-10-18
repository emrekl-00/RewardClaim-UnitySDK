using System.Runtime.CompilerServices;

// Lets the EditMode tests use internal helpers (e.g. setters for config fields)
// without making them part of the public SDK API.
[assembly: InternalsVisibleTo("RewardClaims.Tests.Editor")]
