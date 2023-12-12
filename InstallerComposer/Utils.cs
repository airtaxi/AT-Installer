namespace InstallerComposer;

public static class Utils
{
	public static bool IsPng(byte[] bytes)
	{
		if(bytes.Length < 8) return false;

		return bytes[0] == 137 && bytes[1] == 80 && bytes[2] == 78 && bytes[3] == 71 && bytes[4] == 13 && bytes[5] == 10 && bytes[6] == 26 && bytes[7] == 10;
	}

	public static string RemoveIllegalCharacters(string input) => new(input.Where(c => !Path.GetInvalidFileNameChars().Contains(c)).ToArray());
}

