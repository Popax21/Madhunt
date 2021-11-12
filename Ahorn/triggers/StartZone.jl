module MadhuntStartZone
using ..Ahorn, Maple

@mapdef Trigger "Madhunt/StartZone" StartZone(x::Integer, y::Integer, width::Integer=Maple.defaultTriggerWidth, height::Integer=Maple.defaultTriggerHeight)

const placements = Ahorn.PlacementDict(
    "Start Zone (Madhunt)" => Ahorn.EntityPlacement(
        StartZone,
        "rectangle"
    )
)

end