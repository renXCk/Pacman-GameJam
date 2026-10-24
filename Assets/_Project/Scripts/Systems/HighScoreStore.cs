using System;
using System.IO;
using UnityEngine;

namespace Game.Systems
{
    // Where the high score is kept. An interface so tests (or a future cloud save) can swap it.
    public interface IHighScoreStore
    {
        int Load();
        void Save(int highScore);
    }

    // High score as a small JSON file. A missing or unreadable file means 0, never an exception:
    // a broken save must not stop anyone playing.
    public sealed class JsonHighScoreStore : IHighScoreStore
    {
        // JsonUtility needs a serializable class with public fields.
        [Serializable]
        sealed class SaveData
        {
            public int version = 1;
            public int highScore;
        }

        readonly string path;

        public JsonHighScoreStore(string path)
        {
            this.path = path;
        }

        public int Load()
        {
            try
            {
                if (!File.Exists(path))
                    return 0;

                var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
                return data != null ? Mathf.Max(0, data.highScore) : 0;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"High score file {path} could not be read, starting from 0. {e.Message}");
                return 0;
            }
        }

        public void Save(int highScore)
        {
            try
            {
                string folder = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(folder))
                    Directory.CreateDirectory(folder);

                // Write a temp file first and swap it in, so a crash mid-write cannot corrupt the save.
                string temp = path + ".tmp";
                File.WriteAllText(temp, JsonUtility.ToJson(new SaveData { highScore = Mathf.Max(0, highScore) }));

                if (File.Exists(path))
                    File.Replace(temp, path, null);
                else
                    File.Move(temp, path);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"High score could not be saved to {path}. {e.Message}");
            }
        }
    }
}
