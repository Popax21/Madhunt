module MadhuntHiderWinHeart
using ..Ahorn, Maple

@mapdef Entity "Madhunt/HiderWinHeart" HiderWinHeart(x::Integer, y::Integer)

const placements = Ahorn.PlacementDict(
    "Hider Win Heart (Madhunt)" => Ahorn.EntityPlacement(
        HiderWinHeart
    )
)

const sprite = "collectables/heartGem/0/00.png"

function Ahorn.selection(entity::HiderWinHeart)
    x, y = Ahorn.position(entity)
    return Ahorn.getSpriteRectangle(sprite, x, y)
end

Ahorn.render(ctx::Ahorn.Cairo.CairoContext, entity::HiderWinHeart, room::Maple.Room) = Ahorn.drawSprite(ctx, sprite, 0, 0)

end