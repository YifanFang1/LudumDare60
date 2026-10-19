using System.IO;
using UnityEditor;
using UnityEngine;

// Fight > Audio > Bitcrush Selected Clips: makes a crunchy, lo-fi copy of each selected AudioClip.
//
// The effect is baked into a new file because the WebGL build can't run audio filters live.
// For each selected clip it:
//   1. writes "<name> (crushed).wav" next to it (the original is kept),
//   2. swaps the clip for the crushed copy on every character art asset (CharacterLook) that uses it,
//      so the change shows up in game without dragging anything.
// To try new numbers below, just run it again: selecting a crushed copy works too (it goes back to
// the original and overwrites the copy, so nothing gets crushed twice).
//
// If a clip fails to read, set its Load Type to "Decompress On Load" in the Inspector, Apply, and
// try again.
static class BitcrushAudio
{
    // How strong the effect is. Smaller Bits = grittier; bigger Hold = more muffled and robotic.
    const int Bits = 6;     // 16 is clean CD quality, 8 is old game console, 4 is very harsh
    const int Hold = 3;     // repeat each sample this many times (lowers the sample rate)

    [MenuItem("Fight/Audio/Bitcrush Selected Clips")]
    static void Run()
    {
        int made = 0, swapped = 0;
        System.Collections.Generic.HashSet<AudioClip> done = new System.Collections.Generic.HashSet<AudioClip>();
        foreach (Object o in Selection.objects)
        {
            AudioClip clip = o as AudioClip;
            if (clip == null) continue;

            // A crushed copy: work from its original instead.
            if (clip.name.EndsWith(Suffix))
            {
                clip = FindOriginal(clip);
                if (clip == null) continue;
            }
            if (!done.Add(clip)) continue;   // original and copy both selected
            string path = AssetDatabase.GetAssetPath(clip);

            float[] samples = new float[clip.samples * clip.channels];
            clip.LoadAudioData();
            if (!clip.GetData(samples, 0))
            {
                Debug.LogError("[Bitcrush] Couldn't read " + clip.name + ". Set its Load Type to Decompress On Load, Apply, and try again.", clip);
                continue;
            }

            Crush(samples, clip.channels);
            string outPath = Path.Combine(Path.GetDirectoryName(path), clip.name + Suffix + ".wav").Replace('\\', '/');
            WriteWav(outPath, samples, clip.channels, clip.frequency);
            AssetDatabase.ImportAsset(outPath, ImportAssetOptions.ForceUpdate);   // keeps its .meta, so references survive

            AudioClip crushed = AssetDatabase.LoadAssetAtPath<AudioClip>(outPath);
            if (crushed != null) swapped += SwapOnCharacters(clip, crushed);
            made++;
        }

        if (made == 0) Debug.LogWarning("[Bitcrush] Select one or more audio clips in the Project window first.");
        else Debug.Log("[Bitcrush] Made " + made + " crushed clip(s) and swapped " + swapped + " use(s) on character art assets.");
    }

    const string Suffix = " (crushed)";

    // The clip a crushed copy was made from: same folder, same name without " (crushed)".
    static AudioClip FindOriginal(AudioClip crushed)
    {
        string folder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(crushed)).Replace('\\', '/');
        string name = crushed.name.Substring(0, crushed.name.Length - Suffix.Length);
        foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { folder }))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetDirectoryName(p).Replace('\\', '/') == folder && Path.GetFileNameWithoutExtension(p) == name)
                return AssetDatabase.LoadAssetAtPath<AudioClip>(p);
        }
        Debug.LogWarning("[Bitcrush] Skipped " + crushed.name + ": couldn't find the original \"" + name + "\" next to it.", crushed);
        return null;
    }

    // Lowers the bit depth (rounds every sample to fewer levels) and the sample rate (holds each
    // sample for a few frames).
    static void Crush(float[] samples, int channels)
    {
        float step = 1f / (1 << (Bits - 1));
        float[] held = new float[channels];
        int frames = samples.Length / channels;
        for (int f = 0; f < frames; f++)
        {
            for (int c = 0; c < channels; c++)
            {
                int i = f * channels + c;
                if (f % Hold == 0) held[c] = Mathf.Round(samples[i] / step) * step;
                samples[i] = Mathf.Clamp(held[c], -1f, 1f);
            }
        }
    }

    // Replaces every reference to `from` with `to` on CharacterLook assets (sfx and voice lists).
    static int SwapOnCharacters(AudioClip from, AudioClip to)
    {
        int count = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:CodeCharacterArt t:CharacterData"))
        {
            CharacterLook look = AssetDatabase.LoadAssetAtPath<CharacterLook>(AssetDatabase.GUIDToAssetPath(guid));
            if (look == null) continue;
            SerializedObject so = new SerializedObject(look);
            SerializedProperty p = so.GetIterator();
            bool changed = false;
            while (p.Next(true))
            {
                if (p.propertyType == SerializedPropertyType.ObjectReference && p.objectReferenceValue == from)
                {
                    p.objectReferenceValue = to;
                    changed = true;
                    count++;
                }
            }
            if (changed)
            {
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(look);
                Debug.Log("[Bitcrush] " + look.name + " now uses " + to.name, look);
            }
        }
        AssetDatabase.SaveAssets();
        return count;
    }

    // A plain 16-bit PCM .wav file.
    static void WriteWav(string path, float[] samples, int channels, int frequency)
    {
        using (BinaryWriter w = new BinaryWriter(File.Create(path)))
        {
            int dataBytes = samples.Length * 2;
            w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            w.Write(36 + dataBytes);
            w.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
            w.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
            w.Write(16);
            w.Write((short)1);                     // PCM
            w.Write((short)channels);
            w.Write(frequency);
            w.Write(frequency * channels * 2);     // bytes per second
            w.Write((short)(channels * 2));        // bytes per frame
            w.Write((short)16);                    // bits per sample
            w.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            w.Write(dataBytes);
            for (int i = 0; i < samples.Length; i++) w.Write((short)(Mathf.Clamp(samples[i], -1f, 1f) * 32767f));
        }
    }
}
