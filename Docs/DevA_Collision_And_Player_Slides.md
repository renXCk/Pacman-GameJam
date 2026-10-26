# Deck content: Collision Detection and the player (Dev A, due week 8)

Seven slides. Each has the slide text, a code or diagram suggestion, and speaker notes.
All code below is from the project and can be pasted as it is.

---

## Slide 1. Collision detection: how the player touches the world

**Slide text**
- One capsule, three kinds of contact: solid walls, trigger pickups, and the ghosts' catch
- Built on Unity's `CharacterController` and trigger colliders, no Rigidbody
- Everything is tested in Play Mode

**Speaker notes**
Collision is what makes the maze feel solid and the pickups feel good. We use three different tools for three
different jobs, and keep each one as simple as it can be.

---

## Slide 2. The player is a capsule

**Slide text**
- `CharacterController`: height 1.8 m, radius 0.4 m, feet at the transform
- Walks at 4.5 m/s (a `PlayerConfig` value); ghosts run 3.8 m/s on level 1, so a straight run is slightly faster
- Looks with the mouse: the body turns left and right, the head tilts up and down (limit 80 degrees)

**Diagram**: a side view of the capsule next to a 3 m tile and a 3.3 m wall.

**Speaker notes**
The sizes come from the art guide, so the camera, the corridors and the flashlight all agree. 3 m corridors
leave a lot of room for a 0.8 m wide player, which matters in a game where you are running backwards from ghosts.

---

## Slide 3. Moving with `CharacterController.Move`

**Slide text**
- We call `Move` ourselves every frame, with our own gravity
- `Move` slides along walls instead of stopping, so corners and diagonals feel smooth
- The distance moved is exactly speed times frame time

**Code** (`PlayerController.Apply`)
```csharp
verticalSpeed = body.isGrounded ? GroundedPush : verticalSpeed + Physics.gravity.y * dt;

Vector3 velocity = planarVelocity;
velocity.y = verticalSpeed;
body.Move(velocity * dt);
```

**Speaker notes**
We first tried the simpler `SimpleMove`, but in our speed tests the player covered far less distance than the
configured speed. `Move` with our own gravity gives exactly speed times frame time, which we can test.
A small constant push into the floor keeps `isGrounded` reliable.

---

## Slide 4. Walls: few colliders, solid and smooth

**Slide text**
- The map is text; the builder turns each row of wall tiles into one cube
- Level 1: 246 wall tiles become 92 cubes, each with a box collider
- Walking into a wall at an angle slides along it (tested: `WalkingIntoAWallAtAnAngle_SlidesAlongIt`)
- The player can never leave the map (tested: eight headings, always inside the outer walls)

**Diagram**: the Level 1 map with one run of wall tiles highlighted as one cube.

**Speaker notes**
Merging wall tiles keeps the object count and the NavMesh bake small. One cube per run is the whole trick.

---

## Slide 5. Pickups: triggers, not solids

**Slide text**
- Every cell, flare and battery is a trigger sphere (radius 0.45 m, 0.6 m, 0.5 m) with a small mesh
- The player walks through them; `OnTriggerEnter` fires the right event and hides the pickup
- Only the object tagged `Player` can take one
- No lights on pickups: 189 of them would break the light budget

**Code** (`Pickup.Touch`)
```csharp
public bool Touch(Collider other)
{
    if (other == null || !other.CompareTag(PlayerTag))
        return false;

    PlayerFlashlight flashlight = other.GetComponentInParent<PlayerFlashlight>();
    if (flashlight == null)
        flashlight = other.transform.root.GetComponentInChildren<PlayerFlashlight>();

    return Collect(flashlight);
}
```

**Speaker notes**
A trigger gives us the event without any physical push. A character controller walking into a static trigger
is enough for `OnTriggerEnter`, so no Rigidbody is needed. The test `PickupsNeverBlockThePlayer` walks the
player through two cells and a flare and checks nothing stopped it.

---

## Slide 6. The ghost house door: one door, two rules

**Slide text**
- Ghosts must pass the ghost house door; the player must not
- The door is a solid collider on the Ignore Raycast layer (layer 2)
- The NavMesh bake leaves that layer out, so ghosts path straight through; the player's capsule still hits it
- Tested: `TheGhostHouseDoorBlocksThePlayer`

**Diagram**: top view of the ghost house; green arrow for a ghost, red cross for the player.

**Speaker notes**
We did not want two separate systems for one door. The NavMesh and the physics see the door differently
because of its layer, and that one setting gives both behaviours.

---

## Slide 7. How we know it works

**Slide text**
- Map checks: every map is validated (one start, solid edges, everything reachable, batteries under the house)
- Play Mode tests: slide along walls, blocked by the door, pickups never block, never leave the maze
- Integration tests: finish a level and the next starts with every pickup back; lose all lives, retry, fresh game
- Every pickup is reachable on the NavMesh from the player start

**Speaker notes**
The tests build their own small maze far from the origin, so the real scene cannot interfere. The scene tests
play the real game scene: they take every pickup, get caught on purpose, and retry. Frame time is measured in
a build for the 60 FPS number.

---

### Facts used in these slides, and where they come from

- Capsule 1.8 m by 0.4 m, tile 3 m, wall 3.3 m: art guide.
- 4.5 m/s walking and 3.8 m/s ghosts: `PlayerConfig` and `LevelConfig_Level1` defaults.
- 246 wall tiles, 92 wall cubes, 189 pickups (183 cells, 4 flares, 2 battery spots) in Level 1: counted from `Maze_Level1.txt`.
