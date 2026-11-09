using UnityEngine;
using Object = UnityEngine.Object;

namespace DifficultyMod;

internal sealed class TrophyArt : IDisposable
{
    private readonly Texture2D texture;
    internal Sprite Sprite { get; }

    internal TrophyArt()
    {
        // Verbatim offline export of the game's SPR_Challenges_Trophy sprite.
        // Neutralize the original gold before applying the five UI colors;
        // multiplying a gold image by blue would otherwise produce green.
        using var stream = typeof(TrophyArt).Assembly.GetManifestResourceStream("DifficultyMod.Assets.trophy.png")
            ?? throw new InvalidDataException("Native trophy resource is missing.");
        using var bytes = new MemoryStream();
        stream.CopyTo(bytes);
        texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        texture.hideFlags = HideFlags.HideAndDontSave;
        if (!ImageConversion.LoadImage(texture, bytes.ToArray(), false))
        {
            Object.Destroy(texture);
            throw new InvalidDataException("Native trophy resource cannot be decoded.");
        }
        var pixels = texture.GetPixels32();
        for (int i = 0; i < pixels.Length; i++)
        {
            var pixel = pixels[i];
            byte luminance = (byte)((54 * pixel.r + 183 * pixel.g + 19 * pixel.b) / 256);
            pixels[i] = new Color32(luminance, luminance, luminance, pixel.a);
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        Sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f));
        Sprite.name = "DifficultyNativeTrophy";
        Sprite.hideFlags = HideFlags.HideAndDontSave;
    }

    public void Dispose() { Object.Destroy(Sprite); Object.Destroy(texture); }
}
