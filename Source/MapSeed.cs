using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Verse;

namespace MapReroll {
	public class MapSeed {
		private const string MapRerollSeedSeparator = "|";
		private const string WorldSeedSeparatorPlaceholder = "%PIPE%";
		private const string GeneratedWorldSeedPrefix = "M";
		private const int GeneratedSeedLength = 16;

		private static SHA1CryptoServiceProvider hasher = new SHA1CryptoServiceProvider();

		public static MapSeed TryParse(string seed, out string errorMessage) {
			if (seed.NullOrEmpty()) {
				errorMessage = "No seed provided";
				return null;
			}
			var match = Regex.Match(seed, "^MR(.+)(?=X$)");
			if (!match.Success) {
				errorMessage = "Map Reroll seeds start with \"MR\" and end with \"X\"";
				return null;
			}
			var encodedContents = match.Groups[1].Value;
			if (!MapRerollUtility.TryBase64Decode(encodedContents, out string decodedContents, out string decodeErrorMessage)) {
				errorMessage = $"Failed to decode base64: {decodeErrorMessage}";
				return null;
			}
			var splitContents = decodedContents.Split(MapRerollSeedSeparator[0]);
			if (splitContents.Length != 3) {
				errorMessage = $"Unexpected number of parts ({splitContents.Length})";
				return null;
			}
			var restoredSeed = splitContents[0].Replace(WorldSeedSeparatorPlaceholder, MapRerollSeedSeparator);
			var worldSeed = restoredSeed;
			if (!int.TryParse(splitContents[1], out int worldTile)) {
				errorMessage = $"Failed to parse world tile index ({splitContents[1]})";
				return null;
			}
			if (!int.TryParse(splitContents[2], out int mapSize)) {
				errorMessage = $"Failed to parse map size ({splitContents[2]})";
				return null;
			}
			if (!MapReroll.MapSize.TryResolve(mapSize, out _)) {
				errorMessage = $"Unexpected map size ({mapSize})";
				return null;
			}
			errorMessage = null;
			return new MapSeed(worldSeed, worldTile, mapSize);
		}

		public static MapSeed TryParseLogError(string seed) {
			var result = TryParse(seed, out string errorMessage);
			if(errorMessage != null) MapRerollController.Instance.Logger.Error($"Failed to parse map seed: {errorMessage} ({seed})");
			return result;
		}

		public static MapSeed MakeRandomSeed(int worldTile, int mapSize) {
			var worldSeed = DeriveSHA1Seed(Rand.Int.ToString());
			return new MapSeed(worldSeed, worldTile, mapSize);
		}

		private static string DeriveSHA1Seed(string baseSeed) {
			var oldSeedBytes = Encoding.ASCII.GetBytes(baseSeed);
			var newSeedBytes = hasher.ComputeHash(oldSeedBytes);
			return GeneratedWorldSeedPrefix + Encoding.ASCII.GetString(
						newSeedBytes, 0, GeneratedSeedLength - GeneratedWorldSeedPrefix.Length);
		}

		public string WorldSeed { get; }
		public int WorldTile { get; }
		public int MapSize { get; }

		public MapSeed(string worldSeed, int worldTile, int mapSize) {
			WorldSeed = worldSeed ?? string.Empty;
			WorldTile = worldTile;
			MapSize = mapSize;
		}

		public MapSeed DeriveNextSeed() {
			string newWorldSeed;
			if (int.TryParse(WorldSeed, out _)) {
				// old seed generation, left for backwards compatibility
				// TODO: remove this, leave only new algorithm
				const int magicNumber = 3;
				newWorldSeed = ((WorldSeed.GetHashCode() << 1) * magicNumber).ToString();
			} else {
				newWorldSeed = DeriveSHA1Seed(WorldSeed);
			}
			return new MapSeed(newWorldSeed, WorldTile, MapSize);
		}

		public override string ToString() {
			var s = new StringBuilder();
			s.Append(WorldSeed.Contains(MapRerollSeedSeparator) ? 
				WorldSeed.Replace(MapRerollSeedSeparator, WorldSeedSeparatorPlaceholder) : WorldSeed);
			s.Append(MapRerollSeedSeparator[0]);
			s.Append(WorldTile);
			s.Append(MapRerollSeedSeparator[0]);
			s.Append(MapSize);
			return $"MR{MapRerollUtility.Base64Encode(s.ToString())}X";
		}
	}
}