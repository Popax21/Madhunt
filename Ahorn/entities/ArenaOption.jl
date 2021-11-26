module MadhuntArenaOption
using ..Ahorn, Maple

@mapdef Entity "Madhunt/ArenaOption" ArenaOption(x::Integer, y::Integer, switchID::Integer=0, arenaArea::String="", spawnLevel::String="", spawnIndex::Integer=0, initialSeekers::Integer=1, tagMode::Bool=true, goldenMode::Bool=false)

const placements = Ahorn.PlacementDict(
    "Arena Option (Madhunt)" => Ahorn.EntityPlacement(ArenaOption)
)

function Ahorn.selection(entity::ArenaOption)
    x, y = Ahorn.position(entity)
    return Ahorn.Rectangle(x-8, y-8, 16, 16)
end

Ahorn.render(ctx::Ahorn.Cairo.CairoContext, entity::ArenaOption, room::Maple.Room) = Ahorn.drawSprite(ctx, "ahorn/Madhunt/arenaOption", 0,0, jx=0.5, jy=0.5)

end