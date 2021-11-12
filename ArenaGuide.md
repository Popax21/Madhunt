# Creating your own custom arena
A Madhunt map consists of the following parts:
* The lobby
  * Contains all your start switches. However, these switches need Arena Options to work.
  * Make sure the player spawns here!
* The arena(s)
  * Don't contain any special entites other than Hider/Seeker Spawn Points.

Each of these has to be setup with different custom entites provided by this mod.

## Start Switch
`switchID`: The ID of the switch. Choose any arbitrary number, however make sure that it's unique in the room.

## Start Zone
A trigger which when placed around a start switch creates a zone in which players have to be in to be affected by the switch. By default, switches will affect all players in the same room. Useful for big lobbies.

## Arena Option
Describes one possible arena where a switch can send you. Adding multiple of them will make the start switch choose one at random

`switchID`: The ID of the switch which can choose this option

`arenaArea`: The ID of the map the arena is in. Leave blank for the same map as the option. By default, the A side is choosen, however you can add `#a/b/c` to the end to specify the side.

`spawnLevel`: The name of the room containing the hider and seeker spawn points in the arena map.

`spawnIndex`: The index of the spawn points which will be used. Using negative numbers will randomize that number of indices, e.g. `-3` will randomize between indices `0`,`1` and `2`.

## Hider/Seeker Spawn Point
Provides the location either hiders or seekers spawn in the arena. However, these are only used initialy and when a hider turns into a seeker, regular "Change Respawn" triggers still work!

`spawnIndex`: The index of this spawn point. Matched against the Arena Option's index.
