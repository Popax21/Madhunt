module MadhuntSpawnPoints
using ..Ahorn, Maple

@mapdef Entity "Madhunt/HiderSpawnPoint" HiderSpawnPoint(x::Integer, y::Integer, spawnIndex::Integer=0)
@mapdef Entity "Madhunt/SeekerSpawnPoint" SeekerSpawnPoint(x::Integer, y::Integer, spawnIndex::Integer=0)

const placements = Ahorn.PlacementDict(
    "Hider Spawn Point (Madhunt)" => Ahorn.EntityPlacement(HiderSpawnPoint),
    "Seeker Spawn Point (Madhunt)" => Ahorn.EntityPlacement(SeekerSpawnPoint)
)

sprite = "characters/player/sitDown00.png"

function Ahorn.selection(entity::HiderSpawnPoint)
    x, y = Ahorn.position(entity)
    return Ahorn.getSpriteRectangle(sprite, x, y, jx=0.5, jy=1);
end


function Ahorn.selection(entity::SeekerSpawnPoint)
    x, y = Ahorn.position(entity)
    return Ahorn.getSpriteRectangle(sprite, x, y, jx=0.5, jy=1);
end

function Ahorn.render(ctx::Ahorn.Cairo.CairoContext, entity::HiderSpawnPoint, room::Maple.Room)
    Ahorn.drawSprite(ctx, sprite, 0, 0, jx=0.5, jy=1, tint=(0.0, 1.0, 0.0, 1.0));
end

function Ahorn.render(ctx::Ahorn.Cairo.CairoContext, entity::SeekerSpawnPoint, room::Maple.Room)
    Ahorn.drawSprite(ctx, sprite, 0, 0, jx=0.5, jy=1, tint=(1.0, 0.0, 0.0, 1.0));
end

end