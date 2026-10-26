#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Game.Maze;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests
{
    // Task A7 (optional): warp doors carry the player to their partner. Built far from the origin.
    public sealed class MazeWarpsTests
    {
        const string NoWarps =
            "#########\n" +
            "#...G...#\n" +
            "#.##-##.#\n" +
            "#.#HHH#.#\n" +
            "#.#####.#\n" +
            "#.b.P..o#\n" +
            "#########\n";

        // Row 5 runs from one warp door to the other: x = 0 at the left door, x = 24 at the right one.
        const string OnePair =
            "#########\n" +
            "#...G...#\n" +
            "#.##-##.#\n" +
            "#.#HHH#.#\n" +
            "#.#####.#\n" +
            "W.b.P..oW\n" +
            "#########\n";

        static readonly Vector3 TestOrigin = new Vector3(6000f, 0f, 6000f);

        readonly List<Object> created = new List<Object>();

        [TearDown]
        public void Cleanup()
        {
            foreach (Object item in created)
            {
                if (item != null)
                    Object.DestroyImmediate(item);
            }

            created.Clear();
        }

        MazeWarps MakeMaze(string map, out MazeBuilder builder)
        {
            var config = ScriptableObject.CreateInstance<MazeConfig>();
            created.Add(config);
            SetField(config, "mapFile", new TextAsset(map));

            var go = new GameObject("Test Warp Maze");
            created.Add(go);
            go.SetActive(false);
            go.transform.position = TestOrigin;

            builder = go.AddComponent<MazeBuilder>();
            var warps = go.AddComponent<MazeWarps>();
            SetField(builder, "config", config);

            go.SetActive(true);
            return warps;
        }

        // The player: tagged Player, with a CharacterController like the real one.
        CharacterController MakePlayer(Vector3 feet)
        {
            var go = new GameObject("Test Player");
            created.Add(go);
            go.tag = "Player";
            go.transform.position = feet;

            var body = go.AddComponent<CharacterController>();
            body.height = 1.8f;
            body.radius = 0.4f;
            body.center = new Vector3(0f, 0.9f, 0f);
            return body;
        }

        static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{target.GetType().Name} has no field {name}");
            field.SetValue(target, value);
        }

        // Same place on the floor, ignoring height.
        static void AssertSameSpot(Vector3 actual, Vector3 expected, string what)
        {
            var flat = new Vector2(actual.x - expected.x, actual.z - expected.z);
            Assert.That(flat.magnitude, Is.LessThan(0.05f), $"{what}: expected {expected}, got {actual}");
        }

        [Test]
        public void AMapWithoutWarpDoors_GetsNone()
        {
            MazeWarps warps = MakeMaze(NoWarps, out MazeBuilder builder);

            Assert.That(warps.Doors.Count, Is.EqualTo(0));
            Assert.That(warps.PairCount, Is.EqualTo(0));
            Assert.That(builder.GetComponentsInChildren<WarpDoor>(false).Length, Is.EqualTo(0));
        }

        [Test]
        public void APairOfWarpTiles_BecomesTwoLinkedTriggerDoors()
        {
            MazeWarps warps = MakeMaze(OnePair, out MazeBuilder builder);

            Assert.That(warps.Doors.Count, Is.EqualTo(2));
            Assert.That(warps.PairCount, Is.EqualTo(1));

            WarpDoor left = warps.Doors[0];
            WarpDoor right = warps.Doors[1];
            Assert.That(left.Partner, Is.SameAs(right));
            Assert.That(right.Partner, Is.SameAs(left));
            Assert.That(left.Tile, Is.EqualTo(new TileCoord(5, 0)));
            Assert.That(right.Tile, Is.EqualTo(new TileCoord(5, 8)));

            foreach (WarpDoor door in warps.Doors)
            {
                var box = door.GetComponent<BoxCollider>();
                Assert.That(box, Is.Not.Null);
                Assert.That(box.isTrigger, Is.True);
                Assert.That(door.GetComponentsInChildren<Light>(true).Length, Is.EqualTo(0));
                foreach (Collider collider in door.GetComponentsInChildren<Collider>(true))
                    Assert.That(collider.isTrigger, Is.True, "the pad has no solid collider");
            }

            Vector3 leftExpected = builder.TileCenter(5, 0);
            AssertSameSpot(left.transform.position, leftExpected, "the left door sits on its tile");
            Assert.That(right.transform.position.x - left.transform.position.x, Is.EqualTo(24f).Within(0.01f));
        }

        [Test]
        public void TouchingADoor_CarriesThePlayerToThePartner_AndKeepsTheirHeight()
        {
            MazeWarps warps = MakeMaze(OnePair, out MazeBuilder builder);

            // Parked high above the maze so nothing but this test's calls can touch a door.
            CharacterController player = MakePlayer(TestOrigin + new Vector3(3f, 20f, 3f));

            Assert.That(warps.Touch(warps.Doors[0], player), Is.True);

            Vector3 exit = warps.Doors[1].transform.position;
            AssertSameSpot(player.transform.position, exit, "the player arrives on the partner door");
            Assert.That(player.transform.position.y, Is.EqualTo(20f).Within(0.001f));
            Assert.That(player.enabled, Is.True, "the controller is switched back on after the move");
            Assert.That(warps.WarpCount, Is.EqualTo(1));
        }

        [Test]
        public void ThePlayerIsNotSentStraightBack()
        {
            MazeWarps warps = MakeMaze(OnePair, out MazeBuilder builder);
            CharacterController player = MakePlayer(TestOrigin + new Vector3(3f, 20f, 3f));

            Assert.That(warps.Touch(warps.Doors[0], player), Is.True);
            Vector3 arrived = player.transform.position;

            // They are now inside the other door's trigger. It must not warp them back at once.
            Assert.That(warps.Touch(warps.Doors[1], player), Is.False);
            Assert.That(player.transform.position, Is.EqualTo(arrived));
            Assert.That(warps.WarpCount, Is.EqualTo(1));
        }

        [Test]
        public void OnlyThePlayerIsCarried()
        {
            MazeWarps warps = MakeMaze(OnePair, out MazeBuilder builder);

            var stranger = new GameObject("Not The Player");
            created.Add(stranger);
            stranger.transform.position = TestOrigin + Vector3.up * 20f;
            Collider collider = stranger.AddComponent<BoxCollider>();

            Assert.That(warps.Touch(warps.Doors[0], collider), Is.False);
            Assert.That(warps.Touch(null, collider), Is.False);
            Assert.That(stranger.transform.position, Is.EqualTo(TestOrigin + Vector3.up * 20f));
            Assert.That(warps.WarpCount, Is.EqualTo(0));
        }

        [Test]
        public void RebuildingTheMaze_ReplacesTheDoors()
        {
            MazeWarps warps = MakeMaze(OnePair, out MazeBuilder builder);
            builder.Build();

            Assert.That(warps.Doors.Count, Is.EqualTo(2));
            Assert.That(builder.GetComponentsInChildren<WarpDoor>(false).Length, Is.EqualTo(2), "the old doors are inactive");
        }

        // The real physics path: walking into the left door lands the player at the right one, once.
        [UnityTest]
        public IEnumerator WalkingIntoAWarpDoor_CarriesThePlayerAcross()
        {
            MazeWarps warps = MakeMaze(OnePair, out MazeBuilder builder);

            Vector3 leftDoor = warps.Doors[0].transform.position;
            Vector3 rightDoor = warps.Doors[1].transform.position;
            CharacterController player = MakePlayer(new Vector3(leftDoor.x + 3f, 0f, leftDoor.z));

            float giveUp = Time.realtimeSinceStartup + 3f;
            while (warps.WarpCount == 0 && Time.realtimeSinceStartup < giveUp)
            {
                Vector3 toDoor = new Vector3(leftDoor.x - player.transform.position.x, 0f, 0f);
                player.Move(toDoor.normalized * Mathf.Min(0.1f, toDoor.magnitude));
                yield return new WaitForFixedUpdate();
            }

            Assert.That(warps.WarpCount, Is.EqualTo(1), "walking into the door warped the player");
            AssertSameSpot(player.transform.position, rightDoor, "the player is at the right door");

            // Standing in the other door's trigger does not send them back.
            yield return new WaitForSeconds(1.2f);
            Assert.That(warps.WarpCount, Is.EqualTo(1));
            AssertSameSpot(player.transform.position, rightDoor, "the player is at the right door");
        }
    }
}
#endif
