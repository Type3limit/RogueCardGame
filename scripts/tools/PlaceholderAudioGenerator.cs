using Godot;

namespace RogueCardGame;

/// <summary>
/// Generates placeholder WAV audio files for development.
/// Run once from the editor or at startup to create silence placeholders.
/// These can be replaced with real audio assets later.
/// </summary>
public partial class PlaceholderAudioGenerator : Node
{
    private static readonly string[] BgmFiles =
    {
        "res://resources/audio/bgm/main_menu.wav",
        "res://resources/audio/bgm/map.wav",
        "res://resources/audio/bgm/combat.wav",
        "res://resources/audio/bgm/boss.wav",
        "res://resources/audio/bgm/shop.wav",
        "res://resources/audio/bgm/event.wav",
        "res://resources/audio/bgm/victory.wav",
        "res://resources/audio/bgm/defeat.wav",
    };

    private static readonly string[] SfxFiles =
    {
        "res://resources/audio/sfx/card_play.wav",
        "res://resources/audio/sfx/card_select.wav",
        "res://resources/audio/sfx/card_draw.wav",
        "res://resources/audio/sfx/card_discard.wav",
        "res://resources/audio/sfx/attack_hit.wav",
        "res://resources/audio/sfx/block_gain.wav",
        "res://resources/audio/sfx/heal.wav",
        "res://resources/audio/sfx/status_apply.wav",
        "res://resources/audio/sfx/button_click.wav",
        "res://resources/audio/sfx/button_hover.wav",
        "res://resources/audio/sfx/turn_end.wav",
        "res://resources/audio/sfx/enemy_attack.wav",
        "res://resources/audio/sfx/gold_gain.wav",
        "res://resources/audio/sfx/map_node_select.wav",
    };

    /// <summary>
    /// Generate all placeholder audio files. Call from _Ready() or editor tool.
    /// </summary>
    public static void GenerateAll()
    {
        foreach (var path in BgmFiles)
            GenerateSilenceWav(path, 2.0f); // 2 second BGM loops

        foreach (var path in SfxFiles)
            GenerateSilenceWav(path, 0.2f); // 0.2 second SFX

        GD.Print("[PlaceholderAudioGenerator] Generated all placeholder audio files");
    }

    /// <summary>
    /// Check if placeholder audio files exist, generate if not.
    /// </summary>
    public static void EnsureExists()
    {
        // Check at least one file
        string testPath = ProjectSettings.GlobalizePath(BgmFiles[0]);
        if (System.IO.File.Exists(testPath))
            return;

        GenerateAll();
    }

    private static void GenerateSilenceWav(string resPath, float durationSec)
    {
        string globalPath = ProjectSettings.GlobalizePath(resPath);
        string dir = System.IO.Path.GetDirectoryName(globalPath) ?? "";
        if (!string.IsNullOrEmpty(dir))
            System.IO.Directory.CreateDirectory(dir);

        // Generate a minimal WAV file with silence
        int sampleRate = 22050;
        int channels = 1;
        int bitsPerSample = 16;
        int numSamples = (int)(sampleRate * durationSec);
        int dataSize = numSamples * channels * (bitsPerSample / 8);

        using var stream = new System.IO.FileStream(globalPath, System.IO.FileMode.Create);
        using var writer = new System.IO.BinaryWriter(stream);

        // RIFF header
        writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + dataSize); // file size - 8
        writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));

        // fmt chunk
        writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16);                  // chunk size
        writer.Write((short)1);            // PCM format
        writer.Write((short)channels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * channels * (bitsPerSample / 8)); // byte rate
        writer.Write((short)(channels * (bitsPerSample / 8)));     // block align
        writer.Write((short)bitsPerSample);

        // data chunk
        writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
        writer.Write(dataSize);

        // Write silence (all zeros)
        var silence = new byte[dataSize];
        writer.Write(silence);

        GD.Print($"[Audio] Generated: {resPath}");
    }
}
