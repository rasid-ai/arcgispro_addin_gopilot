using System.Text.Json.Serialization;

namespace Rasid.Models
{
	internal class UserProfile
	{
		[JsonPropertyName("profile")]
		public ProfileData Profile { get; set; }

		[JsonPropertyName("balance")]
		public BalanceData Balance { get; set; }

		public string Name => Profile != null 
			? $"{Profile.FirstName} {Profile.LastName}".Trim() 
			: string.Empty;

		public double Credits => Balance?.Amount ?? Profile?.Balance ?? 0.0;
	}

	internal class ProfileData
	{
		[JsonPropertyName("first_name")]
		public string FirstName { get; set; }

		[JsonPropertyName("last_name")]
		public string LastName { get; set; }

		[JsonPropertyName("email")]
		public string Email { get; set; }

		[JsonPropertyName("username")]
		public string Username { get; set; }

		[JsonPropertyName("balance")]
		public double Balance { get; set; }
	}

	internal class BalanceData
	{
		[JsonPropertyName("amount")]
		public double Amount { get; set; }
	}
}