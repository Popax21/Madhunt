module MadhuntC3OshiroSecret
using ..Ahorn, Maple

@mapdef Entity "Madhunt/C3OshiroSecret" C3OshiroSecret(x::Integer, y::Integer)

const placements = Ahorn.PlacementDict(
    "Chapter 3 Oshiro Secret (Madhunt)" => Ahorn.EntityPlacement(C3OshiroSecret)
)

sprite = "characters/oshiro/oshiro24"

function Ahorn.selection(entity::C3OshiroSecret)
    x, y = Ahorn.position(entity)
    return Ahorn.getSpriteRectangle(sprite, x, y, jx=0.5, jy=1.0)
end

function Ahorn.render(ctx::Ahorn.Cairo.CairoContext, entity::C3OshiroSecret, room::Maple.Room)
    Ahorn.drawSprite(ctx, sprite, 0, 0, jx=0.5, jy=1.0, sx=-1.0)
end

end