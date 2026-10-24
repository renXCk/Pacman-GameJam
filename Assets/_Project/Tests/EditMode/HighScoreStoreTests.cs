using System.IO;
using System.Text.RegularExpressions;
using Game.Systems;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests
{
    // The JSON high score file, in a temp folder so the real save is never touched.
    public class HighScoreStoreTests
    {
        string folder;
        string path;

        [SetUp]
        public void MakeFolder()
        {
            folder = Path.Combine(Path.GetTempPath(), "BlackoutMazeTests_" + Path.GetRandomFileName());
            path = Path.Combine(folder, "highscore.json");
        }

        [TearDown]
        public void RemoveFolder()
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, true);
        }

        [Test]
        public void SavedScoreLoadsBack()
        {
            new JsonHighScoreStore(path).Save(12345);
            new JsonHighScoreStore(path).Save(23456);

            Assert.That(new JsonHighScoreStore(path).Load(), Is.EqualTo(23456));
            Assert.That(File.Exists(path + ".tmp"), Is.False, "the temp file is swapped in, not left behind");
        }

        [Test]
        public void MissingFileMeansZero()
        {
            Assert.That(new JsonHighScoreStore(path).Load(), Is.EqualTo(0));
        }

        [Test]
        public void CorruptFileMeansZeroWithoutThrowing()
        {
            Directory.CreateDirectory(folder);
            File.WriteAllText(path, "{ this is not json");
            LogAssert.Expect(LogType.Warning, new Regex("could not be read"));

            Assert.That(new JsonHighScoreStore(path).Load(), Is.EqualTo(0));
        }

        [Test]
        public void NegativeScoreInFileIsClampedToZero()
        {
            Directory.CreateDirectory(folder);
            File.WriteAllText(path, "{\"version\":1,\"highScore\":-40}");

            Assert.That(new JsonHighScoreStore(path).Load(), Is.EqualTo(0));
        }
    }
}
