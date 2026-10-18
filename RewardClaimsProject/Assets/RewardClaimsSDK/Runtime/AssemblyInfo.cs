using System.Runtime.CompilerServices;

// Lets the EditMode tests use internal helpers (e.g. setters for config fields)
// without making them part of the public SDK API.
[assembly: InternalsVisibleTo("RewardClaims.Tests.Editor")]

// The PlayMode network tests build a ClaimManager by hand, and its constructor (like the wallet's
// and the store's) is internal: games get these objects from the RewardClaimsSdk facade.
[assembly: InternalsVisibleTo("RewardClaims.Tests.PlayMode")]
