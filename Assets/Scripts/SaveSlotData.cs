using System;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// One save slot's record: where the player was, what was on screen and when it was saved.
/// </summary>
[Serializable]
public sealed class SaveSlotData
{
    public int version = 1;
    public int slot;

    public int sceneBuildIndex = -1;
    public string sceneName = "";
    public string sceneTitle = "";

    // How many dialogue lines this scene had started when the save was taken. The scene is
    // loaded and fast-forwarded to this line, which is how the exact place is restored.
    public int lineCount;

    // Choice buttons the player pressed in this scene, in order, so the replay can walk the
    // same route back to the saved line.
    public string choiceRoute = "";

    public string savedAtUtc = "";
    public string speaker = "";
    public string preview = "";
}

/// <summary>
/// Reads and writes the twelve save slots kept in the player's persistent data folder.
/// A slot is a small JSON record plus a screenshot thumbnail, so saves survive a restart.
/// </summary>
public static class SaveSlots
{
    public const int SlotCount = 12;

    const string FolderName = "Saves";
    const int ThumbnailWidth = 480;

    static string FolderPath
    {
        get { return Path.Combine(Application.persistentDataPath, FolderName); }
    }

    static string DataPath(int slot)
    {
        return Path.Combine(FolderPath, "slot_" + slot.ToString("00") + ".json");
    }

    static string ThumbnailPath(int slot)
    {
        return Path.Combine(FolderPath, "slot_" + slot.ToString("00") + ".png");
    }

    public static bool HasSave(int slot)
    {
        if (slot < 1 || slot > SlotCount)
            return false;

        return File.Exists(DataPath(slot));
    }

    public static SaveSlotData Read(int slot)
    {
        if (!HasSave(slot))
            return null;

        try
        {
            string json = File.ReadAllText(DataPath(slot));
            SaveSlotData data = JsonUtility.FromJson<SaveSlotData>(json);

            if (data == null || data.sceneBuildIndex < 0)
                return null;

            if (string.IsNullOrEmpty(data.sceneTitle))
                data.sceneTitle = TitleForScene(data.sceneName);

            return data;
        }
        catch (Exception error)
        {
            Debug.LogWarning("SaveLoad: could not read slot " + slot + " - " + error.Message);
            return null;
        }
    }

    public static bool Write(SaveSlotData data, Texture2D thumbnail)
    {
        if (data == null)
            return false;

        try
        {
            Directory.CreateDirectory(FolderPath);
            File.WriteAllText(DataPath(data.slot), JsonUtility.ToJson(data, true), Encoding.UTF8);

            if (thumbnail != null)
            {
                File.WriteAllBytes(ThumbnailPath(data.slot), thumbnail.EncodeToPNG());
            }
            else if (File.Exists(ThumbnailPath(data.slot)))
            {
                // Overwriting a slot whose screen could not be captured must not leave the old
                // picture behind, or the card would show a scene the save no longer points at.
                File.Delete(ThumbnailPath(data.slot));
            }

            return true;
        }
        catch (Exception error)
        {
            Debug.LogWarning("SaveLoad: could not write slot " + data.slot + " - " + error.Message);
            return false;
        }
    }

    public static Texture2D ReadThumbnail(int slot)
    {
        if (!HasSave(slot))
            return null;

        try
        {
            string path = ThumbnailPath(slot);
            if (!File.Exists(path))
                return null;

            var texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
            if (!texture.LoadImage(File.ReadAllBytes(path)))
            {
                UnityEngine.Object.Destroy(texture);
                return null;
            }

            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            return texture;
        }
        catch (Exception error)
        {
            Debug.LogWarning("SaveLoad: could not read the thumbnail of slot " + slot + " - " + error.Message);
            return null;
        }
    }

    /// <summary>
    /// Shrinks a full-screen capture down to a card-sized picture and returns it. The caller
    /// owns the result and should destroy it after it has been written.
    /// </summary>
    public static Texture2D CreateThumbnail(Texture2D source)
    {
        if (source == null || source.width < 2 || source.height < 2)
            return null;

        int width = ThumbnailWidth;
        int height = Mathf.Max(2, Mathf.RoundToInt(width * (source.height / (float)source.width)));

        RenderTexture target = RenderTexture.GetTemporary(
            width,
            height,
            0,
            RenderTextureFormat.ARGB32,
            RenderTextureReadWrite.sRGB);

        RenderTexture previous = RenderTexture.active;
        Graphics.Blit(source, target);
        RenderTexture.active = target;

        var thumbnail = new Texture2D(width, height, TextureFormat.RGB24, false);
        thumbnail.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
        thumbnail.Apply(false);

        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(target);

        return thumbnail;
    }

    /// <summary>Turns a scene asset name such as "IntoTheWestTube01" into "Into The West Tube 01".</summary>
    public static string TitleForScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
            return "Unknown Scene";

        var builder = new StringBuilder(sceneName.Length + 12);

        for (int i = 0; i < sceneName.Length; i++)
        {
            char current = sceneName[i];

            if (i > 0)
            {
                char previous = sceneName[i - 1];
                bool wordStart = char.IsUpper(current) && !char.IsUpper(previous) && previous != ' ';
                bool numberStart = char.IsDigit(current) && !char.IsDigit(previous) && previous != ' ';

                if (wordStart || numberStart)
                    builder.Append(' ');
            }

            builder.Append(current);
        }

        return builder.ToString();
    }
}
