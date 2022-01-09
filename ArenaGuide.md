# Creating your own custom arena
A Madhunt map consists of the following parts:
* The lobby
  * Contains all your start switches. However, these switches need Arena Options to work.
  * Make sure the player spawns here!
  * Can optionally contain Transition Boosters (+ Targets)
* The arena(s)
  * Don't contain any special entites other than Hider/Seeker Spawn Points.
  * Can optionaly contain gameplay entities like Hider Win Hearts

Each of these has to be setup with different custom entites provided by this mod.

## Start Switch
`switchID`: The ID of the switch. Choose any arbitrary number, however make sure that it's unique in the room.

`name`: The dialogue key displayed above the switch. Leave empty to not display anything.

## Start Zone
A trigger which when placed around a start switch creates a zone in which players have to be in to be affected by the switch. By default, switches will affect all players in the same room. Useful for big lobbies.

## Arena Option
Describes one possible arena where a switch can send you. Adding multiple of them will make the start switch choose one at random

`switchIDs`: The IDs of switches which can choose this option. Individual IDs are seperated by `,`

`arenaArea`: The ID of the map the arena is in. Leave blank for the same map as the option. By default, the A side is choosen, however you can add `#a/b/c` to the end to specify the side.

`spawnLevel`: The name of the room containing the hider and seeker spawn points in the arena map.

`spawnIndex`: The index of the spawn points which will be used. Using negative numbers will randomize that number of indices, e.g. `-3` will randomize between indices `0`,`1` and `2`.

`initialSeekers`: The number of initial seekers. Negative numbers indicate a number of initial hiders instead.

`tagMode`: Seekers can turn hiders into new seekers by touching them.

`goldenMode`: Hiders always turn into seekers when they die.

`hideNames`: Don't show names of seekers to hiders and vice versa.

## Transition Booster
A special kind of booster. If it hits a room transition while moving in the target direction, the player will transition to the Transition Booster Target with the same ID in the target area + room.

`targetDir`: The direction the booster must be moving in to trigger a transition

`targetArea`: The target area of the booster. Format the same as `arenaArea` for Arena Options

`targetLevel`: The target room of the booster. The player will end up in this room

`targetID`: The ID of the booster target in the target room

## Transition Booster Target
A potential target for transition boosters targeting this room. After a transition happens which targets it, the player will appear in a new dummy Transition Booster moving in the specified direction.

`targetID`: The ID of the target

`boosterDir`: The direction the dummy booster will be moving in

## Hider/Seeker Spawn Point
Provides the location either hiders or seekers spawn in the arena. However, these are only used initialy and when a hider turns into a seeker, regular "Change Respawn" triggers still work!

`spawnIndex`: The index of this spawn point. Matched against the Arena Option's index.

## Hider Win Heart
Can only be collected by hiders. When collected, makes all hiders in the current round win.

# Flags
Madhunt sets some flags which you can use in your map:

`Madhunt_InRound`: Set to true if the player is currently in a round

`Madhunt_IsHider`: Set to true if the player is currently a hider

`Madhunt_IsSeeker`: Set to true if the player is currently a seeker