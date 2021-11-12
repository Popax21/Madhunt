module MadhuntStartSwitch
using ..Ahorn, Maple

@mapdef Entity "Madhunt/StartSwitch" StartSwitch(x::Integer, y::Integer, side::Integer=0, switchID::Integer=0)

const placements = Ahorn.PlacementDict(
    "Start Switch (Madhunt)" => Ahorn.EntityPlacement(
        StartSwitch,
        "rectangle"
    )
)

Ahorn.editingOptions(entity::StartSwitch) = Dict{String, Any}(
    "side" => Dict{String, Integer}(
        "up" => 0,
        "down" => 1,
        "left" => 2,
        "right" => 3
    )
)

function Ahorn.selection(entity::StartSwitch)
    x, y = Ahorn.position(entity)
    side = get(entity.data, "side", 0)

    if side == 0 
        return Ahorn.Rectangle(x, y, 16, 12)
    elseif side == 1 
        return Ahorn.Rectangle(x, y - 4, 16, 12)
    elseif side == 2 
        return Ahorn.Rectangle(x, y - 1, 10, 16)
    elseif side == 3 
        return Ahorn.Rectangle(x - 2, y - 1, 10, 16)
    end
end

function Ahorn.render(ctx::Ahorn.Cairo.CairoContext, entity::StartSwitch, room::Maple.Room)
    side = get(entity.data, "side", 0)
    texture = "objects/temple/dashButtonMirror00.png"
    
    if side == 0 
        Ahorn.drawSprite(ctx, texture, 9, 20, rot=-pi / 2)
    elseif side == 1 
        Ahorn.drawSprite(ctx, texture, 27, 7, rot=pi / 2)
    elseif side == 2 
        Ahorn.drawSprite(ctx, texture, 20, 25, rot=pi)
    elseif side == 3 
        Ahorn.drawSprite(ctx, texture, 8, 7)
    end
end

function Ahorn.flipped(entity::StartSwitch, horizontal::Bool)
    side = get(entity.data, "side", 0)
    if side == 0 && !horizontal
        entity.side = 1
        return entity
    elseif side == 1 && !horizontal
        entity.side = 0
        return entity
    elseif side == 2 && horizontal
        entity.side = 3
        return entity
    elseif side == 2 && horizontal
        entity.side = 2
        return entity
    end
end
end